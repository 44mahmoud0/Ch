using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public sealed class TaskExecutor : ITaskExecutor
    {
        private readonly IMissionEventSink _eventSink;

        public TaskExecutor(IMissionEventSink? eventSink = null)
        {
            _eventSink = eventSink ?? NullMissionEventSink.Instance;
        }

        public TaskExecutor(Action<MissionTaskEvent>? eventListener)
            : this(eventListener is null ? null : new DelegateMissionEventSink(eventListener))
        {
        }

        public async Task<TaskExecutionResult> ExecuteAsync(
            string missionId,
            MissionTaskDefinition task,
            CancellationToken missionToken)
        {
            ArgumentNullException.ThrowIfNull(missionId);
            ArgumentNullException.ThrowIfNull(task);

            var stopwatch = Stopwatch.StartNew();
            int attempt = 0;
            string? lastError = null;

            await EmitAsync(new MissionTaskEvent(
                missionId,
                task.Id,
                MissionTaskEventType.Started,
                DateTimeOffset.UtcNow,
                1,
                null)).ConfigureAwait(false);

            while (attempt < task.Retry.MaxAttempts)
            {
                attempt++;

                if (missionToken.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    return await CancelledAsync(
                        missionId,
                        task.Id,
                        attempt,
                        stopwatch.Elapsed,
                        "Cancelled before attempt started.").ConfigureAwait(false);
                }

                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(missionToken);
                if (task.Timeout > TimeSpan.Zero)
                {
                    timeoutSource.CancelAfter(task.Timeout);
                }

                try
                {
                    var success = await task.ExecuteAsync(timeoutSource.Token).ConfigureAwait(false);
                    if (success)
                    {
                        stopwatch.Stop();
                        await EmitAsync(new MissionTaskEvent(
                            missionId,
                            task.Id,
                            MissionTaskEventType.Succeeded,
                            DateTimeOffset.UtcNow,
                            attempt,
                            null)).ConfigureAwait(false);
                        return new TaskExecutionResult(task.Id, TaskExecutionStatus.Succeeded, attempt, stopwatch.Elapsed, null);
                    }

                    lastError = "Task returned false status.";
                }
                catch (OperationCanceledException) when (missionToken.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    return await CancelledAsync(
                        missionId,
                        task.Id,
                        attempt,
                        stopwatch.Elapsed,
                        "Cancelled by mission token.").ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    lastError = "Task timed out.";
                    await EmitAsync(new MissionTaskEvent(
                        missionId,
                        task.Id,
                        MissionTaskEventType.TimedOut,
                        DateTimeOffset.UtcNow,
                        attempt,
                        lastError)).ConfigureAwait(false);

                    if (attempt >= task.Retry.MaxAttempts)
                    {
                        stopwatch.Stop();
                        return new TaskExecutionResult(task.Id, TaskExecutionStatus.TimedOut, attempt, stopwatch.Elapsed, lastError);
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    if (attempt >= task.Retry.MaxAttempts)
                    {
                        stopwatch.Stop();
                        await EmitAsync(new MissionTaskEvent(
                            missionId,
                            task.Id,
                            MissionTaskEventType.Failed,
                            DateTimeOffset.UtcNow,
                            attempt,
                            lastError)).ConfigureAwait(false);
                        return new TaskExecutionResult(task.Id, TaskExecutionStatus.Failed, attempt, stopwatch.Elapsed, lastError);
                    }
                }

                if (attempt < task.Retry.MaxAttempts)
                {
                    await EmitAsync(new MissionTaskEvent(
                        missionId,
                        task.Id,
                        MissionTaskEventType.Retrying,
                        DateTimeOffset.UtcNow,
                        attempt,
                        lastError)).ConfigureAwait(false);

                    try
                    {
                        await Task.Delay(CalculateBackoff(task.Retry, attempt), missionToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        stopwatch.Stop();
                        return await CancelledAsync(
                            missionId,
                            task.Id,
                            attempt,
                            stopwatch.Elapsed,
                            "Cancelled during retry backoff.").ConfigureAwait(false);
                    }
                }
            }

            stopwatch.Stop();
            await EmitAsync(new MissionTaskEvent(
                missionId,
                task.Id,
                MissionTaskEventType.Failed,
                DateTimeOffset.UtcNow,
                attempt,
                lastError ?? "Max attempts reached.")).ConfigureAwait(false);
            return new TaskExecutionResult(task.Id, TaskExecutionStatus.Failed, attempt, stopwatch.Elapsed, lastError ?? "Max attempts reached.");
        }

        private async Task<TaskExecutionResult> CancelledAsync(
            string missionId,
            string taskId,
            int attempt,
            TimeSpan duration,
            string message)
        {
            await EmitAsync(new MissionTaskEvent(
                missionId,
                taskId,
                MissionTaskEventType.Cancelled,
                DateTimeOffset.UtcNow,
                attempt,
                message)).ConfigureAwait(false);
            return new TaskExecutionResult(taskId, TaskExecutionStatus.Cancelled, attempt, duration, message);
        }

        private ValueTask EmitAsync(MissionTaskEvent evt)
        {
            // Lifecycle events must still be persisted after cancellation, so they use a non-cancelled token.
            return _eventSink.EmitAsync(evt, CancellationToken.None);
        }

        private static TimeSpan CalculateBackoff(RetryPolicy policy, int attempt)
        {
            double delayMs = policy.InitialDelay.TotalMilliseconds * Math.Pow(policy.BackoffFactor, attempt - 1);
            delayMs = Math.Min(delayMs, policy.MaxDelay.TotalMilliseconds);

            if (policy.UseJitter)
            {
                var jitter = Random.Shared.NextDouble() * 0.3 + 0.85;
                delayMs *= jitter;
            }

            return TimeSpan.FromMilliseconds(Math.Max(0, delayMs));
        }
    }
}
