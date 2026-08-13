using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public sealed class TaskExecutor
    {
        private readonly Action<MissionTaskEvent>? _eventListener;

        public TaskExecutor(Action<MissionTaskEvent>? eventListener = null)
        {
            _eventListener = eventListener;
        }

        public async Task<TaskExecutionResult> ExecuteAsync(
            string missionId,
            MissionTaskDefinition task,
            CancellationToken missionToken)
        {
            var sw = Stopwatch.StartNew();
            int attempt = 0;
            string? lastError = null;

            _eventListener?.Invoke(new MissionTaskEvent(missionId, task.Id, MissionTaskEventType.Started, DateTimeOffset.UtcNow, 1, null));

            while (attempt < task.Retry.MaxAttempts)
            {
                attempt++;
                missionToken.ThrowIfCancellationRequested();

                using var ctsTimeout = CancellationTokenSource.CreateLinkedTokenSource(missionToken);
                if (task.Timeout > TimeSpan.Zero)
                {
                    ctsTimeout.CancelAfter(task.Timeout);
                }

                try
                {
                    bool success = await task.ExecuteAsync(ctsTimeout.Token);
                    sw.Stop();

                    if (success)
                    {
                        _eventListener?.Invoke(new MissionTaskEvent(missionId, task.Id, MissionTaskEventType.Succeeded, DateTimeOffset.UtcNow, attempt, null));
                        return new TaskExecutionResult(task.Id, TaskExecutionStatus.Succeeded, attempt, sw.Elapsed, null);
                    }
                    else
                    {
                        lastError = "Task returned false status.";
                    }
                }
                catch (OperationCanceledException) when (missionToken.IsCancellationRequested)
                {
                    sw.Stop();
                    _eventListener?.Invoke(new MissionTaskEvent(missionId, task.Id, MissionTaskEventType.Cancelled, DateTimeOffset.UtcNow, attempt, "Cancelled by mission token."));
                    return new TaskExecutionResult(task.Id, TaskExecutionStatus.Cancelled, attempt, sw.Elapsed, "Cancelled.");
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    lastError = "Task timed out.";
                    _eventListener?.Invoke(new MissionTaskEvent(missionId, task.Id, MissionTaskEventType.TimedOut, DateTimeOffset.UtcNow, attempt, lastError));
                    
                    if (attempt >= task.Retry.MaxAttempts)
                    {
                        return new TaskExecutionResult(task.Id, TaskExecutionStatus.TimedOut, attempt, sw.Elapsed, lastError);
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    if (attempt >= task.Retry.MaxAttempts)
                    {
                        sw.Stop();
                        _eventListener?.Invoke(new MissionTaskEvent(missionId, task.Id, MissionTaskEventType.Failed, DateTimeOffset.UtcNow, attempt, lastError));
                        return new TaskExecutionResult(task.Id, TaskExecutionStatus.Failed, attempt, sw.Elapsed, lastError);
                    }
                }

                if (attempt < task.Retry.MaxAttempts)
                {
                    _eventListener?.Invoke(new MissionTaskEvent(missionId, task.Id, MissionTaskEventType.Retrying, DateTimeOffset.UtcNow, attempt, lastError));
                    
                    var delay = CalculateBackoff(task.Retry, attempt);
                    try
                    {
                        await Task.Delay(delay, missionToken);
                    }
                    catch (OperationCanceledException)
                    {
                        sw.Stop();
                        return new TaskExecutionResult(task.Id, TaskExecutionStatus.Cancelled, attempt, sw.Elapsed, "Cancelled during backoff.");
                    }
                }
            }

            sw.Stop();
            _eventListener?.Invoke(new MissionTaskEvent(missionId, task.Id, MissionTaskEventType.Failed, DateTimeOffset.UtcNow, attempt, lastError));
            return new TaskExecutionResult(task.Id, TaskExecutionStatus.Failed, attempt, sw.Elapsed, lastError ?? "Max attempts reached.");
        }

        private static TimeSpan CalculateBackoff(RetryPolicy policy, int attempt)
        {
            double delayMs = policy.InitialDelay.TotalMilliseconds * Math.Pow(policy.BackoffFactor, attempt - 1);
            if (delayMs > policy.MaxDelay.TotalMilliseconds)
            {
                delayMs = policy.MaxDelay.TotalMilliseconds;
            }

            if (policy.UseJitter)
            {
                var jitter = Random.Shared.NextDouble() * 0.3 + 0.85; // 85% to 115%
                delayMs *= jitter;
            }

            return TimeSpan.FromMilliseconds(Math.Max(0, delayMs));
        }
    }
}
