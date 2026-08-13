using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public sealed class TaskGraphScheduler
    {
        private readonly TaskExecutor _executor;
        private readonly Action<MissionTaskEvent>? _eventListener;

        public TaskGraphScheduler(TaskExecutor? executor = null, Action<MissionTaskEvent>? eventListener = null)
        {
            _eventListener = eventListener;
            _executor = executor ?? new TaskExecutor(_eventListener);
        }

        public async Task<GraphExecutionResult> ExecuteGraphAsync(
            string missionId,
            IEnumerable<MissionTaskDefinition> taskDefinitions,
            int maxConcurrency = 4,
            CancellationToken cancellationToken = default)
        {
            var tasks = taskDefinitions.ToList();
            GraphValidator.Validate(tasks);

            var startedAt = DateTimeOffset.UtcNow;
            var taskMap = tasks.ToDictionary(t => t.Id, t => t);
            
            var pending = new HashSet<string>(tasks.Select(t => t.Id));
            var running = new Dictionary<string, Task<TaskExecutionResult>>();
            var results = new Dictionary<string, TaskExecutionResult>();

            foreach (var id in pending)
            {
                _eventListener?.Invoke(new MissionTaskEvent(missionId, id, MissionTaskEventType.Queued, startedAt, 0, null));
            }

            bool isCancelled = false;

            try
            {
                while (pending.Count > 0 || running.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Start ready tasks up to maxConcurrency
                    while (running.Count < maxConcurrency)
                    {
                        var readyId = pending.FirstOrDefault(id => AreDependenciesSatisfied(id, taskMap, results));
                        if (readyId == null) break;

                        pending.Remove(readyId);
                        var def = taskMap[readyId];

                        var workerTask = _executor.ExecuteAsync(missionId, def, cancellationToken);
                        running[readyId] = workerTask;
                    }

                    if (running.Count == 0)
                    {
                        // If nothing is running and pending is not empty, graph is stalled due to unmet deps / failures
                        break;
                    }

                    // Task.WhenAny instead of polling
                    var finishedTask = await Task.WhenAny(running.Values);
                    running = running.Where(kv => kv.Value != finishedTask).ToDictionary(kv => kv.Key, kv => kv.Value);

                    var res = await finishedTask;
                    results[res.TaskId] = res;

                    // Propagate dependency failures if needed
                    PropagateFailures(taskMap, results, pending);
                }
            }
            catch (OperationCanceledException)
            {
                isCancelled = true;
            }

            // Mark any remaining pending tasks as Cancelled or Skipped
            foreach (var id in pending.ToList())
            {
                if (!results.ContainsKey(id))
                {
                    var status = isCancelled ? TaskExecutionStatus.Cancelled : TaskExecutionStatus.SkippedDependencyFailure;
                    results[id] = new TaskExecutionResult(id, status, 0, TimeSpan.Zero, "Graph terminated before execution.");
                    _eventListener?.Invoke(new MissionTaskEvent(missionId, id, MissionTaskEventType.Skipped, DateTimeOffset.UtcNow, 0, "Dependency or graph termination."));
                }
                pending.Remove(id);
            }

            // Ensure absolute worker quiescence
            if (running.Count > 0)
            {
                try
                {
                    await Task.WhenAll(running.Values);
                }
                catch
                {
                    // Ignore background faults during final draining
                }
            }

            var finishedAt = DateTimeOffset.UtcNow;
            
            GraphExecutionStatus graphStatus;
            if (isCancelled)
            {
                graphStatus = GraphExecutionStatus.Cancelled;
            }
            else if (results.Values.Any(r => r.Status == TaskExecutionStatus.Failed || r.Status == TaskExecutionStatus.TimedOut))
            {
                graphStatus = GraphExecutionStatus.Failed;
            }
            else if (pending.Count > 0)
            {
                graphStatus = GraphExecutionStatus.Stalled;
            }
            else
            {
                graphStatus = GraphExecutionStatus.Completed;
            }

            return new GraphExecutionResult(graphStatus, results, startedAt, finishedAt);
        }

        private static bool AreDependenciesSatisfied(string taskId, Dictionary<string, MissionTaskDefinition> taskMap, Dictionary<string, TaskExecutionResult> results)
        {
            var def = taskMap[taskId];
            foreach (var dep in def.Dependencies)
            {
                if (!results.TryGetValue(dep, out var res)) return false;
                if (res.Status != TaskExecutionStatus.Succeeded) return false;
            }
            return true;
        }

        private static void PropagateFailures(Dictionary<string, MissionTaskDefinition> taskMap, Dictionary<string, TaskExecutionResult> results, HashSet<string> pending)
        {
            // Check if any pending tasks have dependencies that failed or were skipped
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var id in pending.ToList())
                {
                    var def = taskMap[id];
                    foreach (var dep in def.Dependencies)
                    {
                        if (results.TryGetValue(dep, out var res) && res.Status != TaskExecutionStatus.Succeeded)
                        {
                            pending.Remove(id);
                            results[id] = new TaskExecutionResult(id, TaskExecutionStatus.SkippedDependencyFailure, 0, TimeSpan.Zero, $"Dependency '{dep}' failed or skipped.");
                            changed = true;
                            break;
                        }
                    }
                }
            }
        }
    }
}
