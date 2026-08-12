using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Engine
{
    public enum TaskStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled,
        TimedOut
    }

    public class MissionTask
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string Name { get; init; } = string.Empty;
        public Func<CancellationToken, Task<bool>> Action { get; init; } = _ => Task.FromResult(true);
        public List<string> Dependencies { get; init; } = new();
        public TaskStatus Status { get; set; } = TaskStatus.Pending;
        public string? Error { get; set; }
        public int MaxRetries { get; init; } = 0;
        public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
    }

    public class TaskGraphEngine
    {
        private readonly ILogger<TaskGraphEngine> _logger;

        public TaskGraphEngine(ILogger<TaskGraphEngine> logger)
        {
            _logger = logger;
        }

        public async Task<bool> ExecuteGraphAsync(IEnumerable<MissionTask> tasks, CancellationToken cancellationToken)
        {
            var taskDict = new Dictionary<string, MissionTask>();
            foreach (var t in tasks)
            {
                taskDict[t.Id] = t;
            }

            var completed = new HashSet<string>();
            var running = new HashSet<string>();
            var failed = new HashSet<string>();

            while (completed.Count + failed.Count < taskDict.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool progressMade = false;

                foreach (var kvp in taskDict)
                {
                    var task = kvp.Value;
                    if (task.Status != TaskStatus.Pending) continue;

                    bool depsSatisfied = true;
                    bool depsFailed = false;

                    foreach (var dep in task.Dependencies)
                    {
                        if (failed.Contains(dep))
                        {
                            depsFailed = true;
                            break;
                        }
                        if (!completed.Contains(dep))
                        {
                            depsSatisfied = false;
                            break;
                        }
                    }

                    if (depsFailed)
                    {
                        task.Status = TaskStatus.Cancelled;
                        task.Error = "Dependency task failed.";
                        lock (failed) { failed.Add(task.Id); }
                        progressMade = true;
                        continue;
                    }

                    if (depsSatisfied)
                    {
                        bool shouldRun = false;
                        lock (running)
                        {
                            if (!running.Contains(task.Id))
                            {
                                running.Add(task.Id);
                                shouldRun = true;
                            }
                        }

                        if (shouldRun)
                        {
                            progressMade = true;

                        _ = Task.Run(async () =>
                        {
                            int attempt = 0;
                            bool success = false;

                            while (attempt <= task.MaxRetries && !success)
                            {
                                attempt++;
                                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                                cts.CancelAfter(task.Timeout);

                                try
                                {
                                    task.Status = TaskStatus.Running;
                                    _logger.LogInformation("Executing task {TaskId}: {TaskName} (Attempt {Attempt}/{MaxRetries})", task.Id, task.Name, attempt, task.MaxRetries + 1);
                                    
                                    success = await task.Action(cts.Token);
                                    if (success)
                                    {
                                        task.Status = TaskStatus.Completed;
                                        lock (completed) { completed.Add(task.Id); }
                                        break;
                                    }
                                }
                                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                                {
                                    task.Status = TaskStatus.TimedOut;
                                    task.Error = $"Task timed out after {task.Timeout.TotalSeconds} seconds.";
                                    _logger.LogWarning("Task {TaskId} timed out", task.Id);
                                    break;
                                }
                                catch (Exception ex) when (attempt <= task.MaxRetries)
                                {
                                    _logger.LogWarning(ex, "Task {TaskId} attempt {Attempt} failed, retrying...", task.Id, attempt);
                                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    task.Status = TaskStatus.Failed;
                                    task.Error = ex.Message;
                                    _logger.LogError(ex, "Task {TaskId} failed permanently", task.Id);
                                }
                            }

                            if (!success && task.Status != TaskStatus.TimedOut && task.Status != TaskStatus.Failed)
                            {
                                task.Status = TaskStatus.Failed;
                                task.Error = "Task failed after all retry attempts.";
                            }

                            if (task.Status == TaskStatus.Failed || task.Status == TaskStatus.TimedOut)
                            {
                                lock (failed) { failed.Add(task.Id); }
                            }

                            lock (running) { running.Remove(task.Id); }
                        }, cancellationToken);
                    }
                }

                if (!progressMade && running.Count == 0)
                {
                    _logger.LogError("Task graph execution stalled due to unresolved dependencies or permanent failure.");
                    return false;
                }

                await Task.Delay(50, cancellationToken);
            }

            return failed.Count == 0;
        }
    }
}
