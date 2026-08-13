using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public sealed class TaskGraphScheduler
    {
        private readonly ITaskExecutor _executor;
        private readonly IMissionEventSink _eventSink;

        public TaskGraphScheduler(ITaskExecutor? executor = null, IMissionEventSink? eventSink = null)
        {
            _eventSink = eventSink ?? NullMissionEventSink.Instance;
            _executor = executor ?? new TaskExecutor(_eventSink);
        }

        public TaskGraphScheduler(ITaskExecutor? executor, Action<MissionTaskEvent>? eventListener)
            : this(executor, eventListener is null ? null : new DelegateMissionEventSink(eventListener))
        {
        }

        public async Task<GraphExecutionResult> ExecuteGraphAsync(
            string missionId,
            IEnumerable<MissionTaskDefinition> taskDefinitions,
            int maxConcurrency = 4,
            CancellationToken cancellationToken = default)
        {
            if (maxConcurrency < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "maxConcurrency must be at least 1.");
            }

            ArgumentNullException.ThrowIfNull(missionId);
            ArgumentNullException.ThrowIfNull(taskDefinitions);

            var tasks = taskDefinitions.ToList();
            GraphValidator.Validate(tasks);

            var startedAt = DateTimeOffset.UtcNow;
            var taskMap = tasks.ToDictionary(task => task.Id, task => task);
            var pending = new HashSet<string>(tasks.Select(task => task.Id));
            var running = new Dictionary<string, Task<TaskExecutionResult>>();
            var results = new Dictionary<string, TaskExecutionResult>();
            var isCancelled = false;
            var isStalled = false;

            foreach (var task in tasks)
            {
                await EmitAsync(new MissionTaskEvent(
                    missionId,
                    task.Id,
                    MissionTaskEventType.Queued,
                    startedAt,
                    0,
                    null)).ConfigureAwait(false);
            }

            try
            {
                while (pending.Count > 0 || running.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    while (running.Count < maxConcurrency)
                    {
                        var readyTask = tasks.FirstOrDefault(task =>
                            pending.Contains(task.Id) &&
                            AreDependenciesSatisfied(task, results));

                        if (readyTask is null)
                        {
                            break;
                        }

                        pending.Remove(readyTask.Id);
                        running[readyTask.Id] = _executor.ExecuteAsync(missionId, readyTask, cancellationToken);
                    }

                    if (running.Count == 0)
                    {
                        // No task can make progress while pending work remains: this is a real graph stall.
                        isStalled = pending.Count > 0;
                        break;
                    }

                    var finishedTask = await Task.WhenAny(running.Values).ConfigureAwait(false);
                    var finishedEntry = running.First(entry => ReferenceEquals(entry.Value, finishedTask));
                    running.Remove(finishedEntry.Key);

                    var result = await ReadWorkerResultAsync(
                        finishedEntry.Key,
                        finishedTask,
                        isCancelled: false).ConfigureAwait(false);
                    results[result.TaskId] = result;

                    await PropagateFailuresAsync(missionId, taskMap, results, pending).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                isCancelled = true;
            }

            // Cancellation must not discard results from workers that were already started.
            await DrainRunningWorkersAsync(missionId, running, results, isCancelled: isCancelled).ConfigureAwait(false);
            running.Clear();

            // Give every task a terminal result. Pending work is cancelled only when the mission was cancelled;
            // otherwise it is skipped because the graph is stalled or its dependency chain failed.
            foreach (var id in pending.ToArray())
            {
                var status = isCancelled
                    ? TaskExecutionStatus.Cancelled
                    : TaskExecutionStatus.SkippedDependencyFailure;
                var eventType = isCancelled
                    ? MissionTaskEventType.Cancelled
                    : MissionTaskEventType.Skipped;
                var message = isCancelled
                    ? "Mission cancellation prevented execution."
                    : "Task could not execute because the graph was stalled or a dependency failed.";

                results[id] = new TaskExecutionResult(id, status, 0, TimeSpan.Zero, message);
                await EmitAsync(new MissionTaskEvent(
                    missionId,
                    id,
                    eventType,
                    DateTimeOffset.UtcNow,
                    0,
                    message)).ConfigureAwait(false);
            }
            pending.Clear();

            var finishedAt = DateTimeOffset.UtcNow;
            var graphStatus = isCancelled
                ? GraphExecutionStatus.Cancelled
                : isStalled
                    ? GraphExecutionStatus.Stalled
                    : results.Values.Any(result =>
                        result.Status is TaskExecutionStatus.Failed or TaskExecutionStatus.TimedOut)
                        ? GraphExecutionStatus.Failed
                        : GraphExecutionStatus.Completed;

            return new GraphExecutionResult(graphStatus, results, startedAt, finishedAt);
        }

        private static bool AreDependenciesSatisfied(
            MissionTaskDefinition task,
            IReadOnlyDictionary<string, TaskExecutionResult> results)
        {
            foreach (var dependencyId in task.Dependencies)
            {
                if (!results.TryGetValue(dependencyId, out var dependencyResult) ||
                    dependencyResult.Status != TaskExecutionStatus.Succeeded)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task PropagateFailuresAsync(
            string missionId,
            IReadOnlyDictionary<string, MissionTaskDefinition> taskMap,
            IDictionary<string, TaskExecutionResult> results,
            ISet<string> pending)
        {
            bool changed;
            do
            {
                changed = false;
                foreach (var id in pending.ToArray())
                {
                    var task = taskMap[id];
                    var failedDependency = task.Dependencies
                        .Select(dependencyId => results.TryGetValue(dependencyId, out var result)
                            ? (dependencyId, result)
                            : (dependencyId, result: null))
                        .FirstOrDefault(item => item.result is not null && item.result.Status != TaskExecutionStatus.Succeeded);

                    if (failedDependency.result is null)
                    {
                        continue;
                    }

                    pending.Remove(id);
                    var message = $"Dependency '{failedDependency.dependencyId}' failed or was cancelled.";
                    results[id] = new TaskExecutionResult(
                        id,
                        TaskExecutionStatus.SkippedDependencyFailure,
                        0,
                        TimeSpan.Zero,
                        message);
                    await EmitAsync(new MissionTaskEvent(
                        missionId,
                        id,
                        MissionTaskEventType.Skipped,
                        DateTimeOffset.UtcNow,
                        0,
                        message)).ConfigureAwait(false);
                    changed = true;
                }
            } while (changed);
        }

        private async Task DrainRunningWorkersAsync(
            string missionId,
            IReadOnlyDictionary<string, Task<TaskExecutionResult>> running,
            IDictionary<string, TaskExecutionResult> results,
            bool isCancelled)
        {
            foreach (var entry in running.ToArray())
            {
                if (results.ContainsKey(entry.Key))
                {
                    continue;
                }

                var result = await ReadWorkerResultAsync(entry.Key, entry.Value, isCancelled).ConfigureAwait(false);
                results[result.TaskId] = result;
            }
        }

        private static async Task<TaskExecutionResult> ReadWorkerResultAsync(
            string taskId,
            Task<TaskExecutionResult> worker,
            bool isCancelled)
        {
            try
            {
                return await worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                var message = isCancelled ? "Worker cancelled during mission shutdown." : "Worker cancelled.";
                return new TaskExecutionResult(taskId, TaskExecutionStatus.Cancelled, 0, TimeSpan.Zero, message);
            }
            catch (Exception ex)
            {
                return new TaskExecutionResult(taskId, TaskExecutionStatus.Failed, 0, TimeSpan.Zero, ex.Message);
            }
        }

        private ValueTask EmitAsync(MissionTaskEvent evt)
        {
            return _eventSink.EmitAsync(evt, CancellationToken.None);
        }
    }
}
