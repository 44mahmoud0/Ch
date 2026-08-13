using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MahmoudAI.Core.Engine.TaskGraph;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public class TaskGraphV2Tests
    {
        [Fact]
        public async Task Scheduler_ShouldExecuteIndependentTasksInParallel_AndRespectDependencies()
        {
            var events = new ConcurrentQueue<MissionTaskEvent>();
            var scheduler = new TaskGraphScheduler(null, new DelegateMissionEventSink(evt => events.Enqueue(evt)));
            var definitions = new List<MissionTaskDefinition>
            {
                NewTask("t1", async ct => { await Task.Delay(20, ct); return true; }),
                NewTask("t2", async ct => { await Task.Delay(20, ct); return true; }),
                NewTask("t3", async ct => { await Task.Delay(10, ct); return true; }, "t1", "t2")
            };

            var result = await ExecuteAsync(scheduler, definitions, 2);

            result.Status.Should().Be(GraphExecutionStatus.Completed);
            result.Tasks.Should().HaveCount(3);
            result.Tasks.Values.Should().OnlyContain(task => task.Status == TaskExecutionStatus.Succeeded);
            events.Should().Contain(evt => evt.Type == MissionTaskEventType.Queued && evt.TaskId == "t3");
        }

        [Fact]
        public async Task Scheduler_ShouldDetectCyclesAndThrow()
        {
            var scheduler = new TaskGraphScheduler();
            var definitions = new[]
            {
                NewTask("t1", _ => Task.FromResult(true), "t2"),
                NewTask("t2", _ => Task.FromResult(true), "t1")
            };

            Func<Task> act = () => scheduler.ExecuteGraphAsync("m1", definitions);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Scheduler_ShouldPropagateDependencyFailureAsSkipped_AndEmitSkipped()
        {
            var events = new ConcurrentQueue<MissionTaskEvent>();
            var scheduler = new TaskGraphScheduler(null, new DelegateMissionEventSink(evt => events.Enqueue(evt)));
            var definitions = new[]
            {
                NewTask("t1", _ => Task.FromResult(false)),
                NewTask("t2", _ => Task.FromResult(true), "t1")
            };

            var result = await ExecuteAsync(scheduler, definitions, 2);

            result.Status.Should().Be(GraphExecutionStatus.Failed);
            result.Tasks["t1"].Status.Should().Be(TaskExecutionStatus.Failed);
            result.Tasks["t2"].Status.Should().Be(TaskExecutionStatus.SkippedDependencyFailure);
            events.Should().Contain(evt => evt.TaskId == "t2" && evt.Type == MissionTaskEventType.Skipped);
        }

        [Fact]
        public async Task Scheduler_ShouldRespectRetryAndTimeout()
        {
            int attempts = 0;
            var definition = NewTask(
                "retrying",
                _ =>
                {
                    attempts++;
                    return Task.FromResult(attempts >= 3);
                },
                retry: new RetryPolicy(3, TimeSpan.FromMilliseconds(10), 2, TimeSpan.FromMilliseconds(50), false));

            var result = await ExecuteAsync(new TaskGraphScheduler(), new[] { definition }, 1);

            result.Status.Should().Be(GraphExecutionStatus.Completed);
            result.Tasks["retrying"].Status.Should().Be(TaskExecutionStatus.Succeeded);
            result.Tasks["retrying"].Attempts.Should().Be(3);
        }

        [Fact]
        public async Task Scheduler_Cancellation_ShouldReturnResultForEveryTask()
        {
            using var cancellation = new CancellationTokenSource();
            var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int started = 0;
            int stopped = 0;
            var definitions = Enumerable.Range(1, 3)
                .Select(index => NewTask($"t{index}", async ct =>
                {
                    if (Interlocked.Increment(ref started) == 3)
                    {
                        allStarted.TrySetResult();
                    }

                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                        return true;
                    }
                    finally
                    {
                        Interlocked.Increment(ref stopped);
                    }
                }))
                .ToArray();

            var schedulerTask = new TaskGraphScheduler().ExecuteGraphAsync("cancelled", definitions, 3, cancellation.Token);
            await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();

            var result = await schedulerTask;

            result.Status.Should().Be(GraphExecutionStatus.Cancelled);
            result.Tasks.Should().HaveCount(3);
            result.Tasks.Values.Should().OnlyContain(task => task.Status == TaskExecutionStatus.Cancelled);
            stopped.Should().Be(3);
        }

        [Fact]
        public async Task Scheduler_ShouldWaitForAllRunningWorkersOnCancellation()
        {
            using var cancellation = new CancellationTokenSource();
            var workersFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int finished = 0;
            var definitions = Enumerable.Range(1, 4)
                .Select(index => NewTask($"t{index}", async ct =>
                {
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                        return true;
                    }
                    finally
                    {
                        if (Interlocked.Increment(ref finished) == 4)
                        {
                            workersFinished.TrySetResult();
                        }
                    }
                }))
                .ToArray();

            var schedulerTask = new TaskGraphScheduler().ExecuteGraphAsync("drain", definitions, 4, cancellation.Token);
            await Task.Delay(30);
            cancellation.Cancel();
            var result = await schedulerTask;

            await workersFinished.Task.WaitAsync(TimeSpan.FromSeconds(2));
            result.Tasks.Values.Should().OnlyContain(task => task.Status == TaskExecutionStatus.Cancelled);
            finished.Should().Be(4);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Scheduler_ShouldRejectInvalidConcurrency(int maxConcurrency)
        {
            var act = () => new TaskGraphScheduler().ExecuteGraphAsync(
                "invalid-concurrency",
                new[] { NewTask("t1", _ => Task.FromResult(true)) },
                maxConcurrency);

            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }

        [Fact]
        public async Task Scheduler_ShouldReportStalledCorrectly_WhenInjectedExecutorViolatesTaskIdentity()
        {
            var scheduler = new TaskGraphScheduler(new MisreportingExecutor());
            var definitions = new[]
            {
                NewTask("t1", _ => Task.FromResult(true)),
                NewTask("t2", _ => Task.FromResult(true), "t1")
            };

            var result = await ExecuteAsync(scheduler, definitions, 1);

            result.Status.Should().Be(GraphExecutionStatus.Stalled);
            result.Tasks["t2"].Status.Should().Be(TaskExecutionStatus.SkippedDependencyFailure);
        }

        [Fact]
        public async Task Executor_DurationShouldIncludeAllAttemptsAndBackoff()
        {
            int attempts = 0;
            var task = NewTask(
                "duration",
                async _ =>
                {
                    await Task.Delay(10);
                    return ++attempts == 3;
                },
                retry: new RetryPolicy(3, TimeSpan.FromMilliseconds(20), 1, TimeSpan.FromMilliseconds(20), false));

            var result = await new TaskExecutor().ExecuteAsync("duration", task, CancellationToken.None);

            result.Status.Should().Be(TaskExecutionStatus.Succeeded);
            result.Attempts.Should().Be(3);
            result.Duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public void RetryPolicy_ShouldRejectZeroMaxAttempts()
        {
            var act = () => new RetryPolicy(0, TimeSpan.Zero, 1, TimeSpan.Zero, false);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void RetryPolicy_ShouldRejectNegativeDelaysAndNonPositiveBackoff()
        {
            Action negativeInitial = () => new RetryPolicy(1, TimeSpan.FromMilliseconds(-1), 1, TimeSpan.Zero, false);
            Action negativeMax = () => new RetryPolicy(1, TimeSpan.Zero, 1, TimeSpan.FromMilliseconds(-1), false);
            Action zeroBackoff = () => new RetryPolicy(1, TimeSpan.Zero, 0, TimeSpan.Zero, false);

            negativeInitial.Should().Throw<ArgumentOutOfRangeException>();
            negativeMax.Should().Throw<ArgumentOutOfRangeException>();
            zeroBackoff.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public async Task Executor_ShouldEmitCancelledDuringBackoff()
        {
            var sink = new RecordingEventSink();
            using var cancellation = new CancellationTokenSource();
            var task = NewTask(
                "backoff-cancel",
                _ => Task.FromResult(false),
                retry: new RetryPolicy(3, TimeSpan.FromSeconds(2), 1, TimeSpan.FromSeconds(2), false));

            var execution = new TaskExecutor(sink).ExecuteAsync("backoff", task, cancellation.Token);
            await sink.WaitForAsync(MissionTaskEventType.Retrying, "backoff-cancel");
            cancellation.Cancel();
            var result = await execution;

            result.Status.Should().Be(TaskExecutionStatus.Cancelled);
            sink.Events.Should().Contain(evt => evt.Type == MissionTaskEventType.Cancelled && evt.TaskId == "backoff-cancel");
        }

        [Fact]
        public async Task Scheduler_MaxConcurrencyShouldNeverBeExceeded()
        {
            int active = 0;
            int maximum = 0;
            var definitions = Enumerable.Range(1, 8)
                .Select(index => NewTask($"t{index}", async ct =>
                {
                    var current = Interlocked.Increment(ref active);
                    InterlockedExtensions.Max(ref maximum, current);
                    await Task.Delay(25, ct);
                    Interlocked.Decrement(ref active);
                    return true;
                }))
                .ToArray();

            var result = await ExecuteAsync(new TaskGraphScheduler(), definitions, 3);

            result.Status.Should().Be(GraphExecutionStatus.Completed);
            maximum.Should().BeLessThanOrEqualTo(3);
        }

        private static MissionTaskDefinition NewTask(
            string id,
            Func<CancellationToken, Task<bool>> execute,
            params string[] dependencies)
        {
            return new MissionTaskDefinition(
                id,
                id,
                dependencies,
                execute,
                TimeSpan.FromSeconds(2),
                new RetryPolicy(1, TimeSpan.Zero, 1, TimeSpan.Zero, false));
        }

        private static MissionTaskDefinition NewTask(
            string id,
            Func<CancellationToken, Task<bool>> execute,
            string dependency,
            string secondDependency,
            RetryPolicy? retry = null)
        {
            return new MissionTaskDefinition(
                id,
                id,
                new[] { dependency, secondDependency },
                execute,
                TimeSpan.FromSeconds(2),
                retry ?? new RetryPolicy(1, TimeSpan.Zero, 1, TimeSpan.Zero, false));
        }

        private static MissionTaskDefinition NewTask(
            string id,
            Func<CancellationToken, Task<bool>> execute,
            RetryPolicy retry)
        {
            return new MissionTaskDefinition(
                id,
                id,
                Array.Empty<string>(),
                execute,
                TimeSpan.FromSeconds(2),
                retry);
        }

        private static async Task<GraphExecutionResult> ExecuteAsync(
            TaskGraphScheduler scheduler,
            IEnumerable<MissionTaskDefinition> definitions,
            int maxConcurrency)
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await scheduler.ExecuteGraphAsync("m1", definitions, maxConcurrency, cancellation.Token);
        }

        private sealed class MisreportingExecutor : ITaskExecutor
        {
            public Task<TaskExecutionResult> ExecuteAsync(
                string missionId,
                MissionTaskDefinition task,
                CancellationToken missionToken)
            {
                return Task.FromResult(new TaskExecutionResult(
                    "unexpected-task-id",
                    TaskExecutionStatus.Succeeded,
                    1,
                    TimeSpan.Zero,
                    null));
            }
        }

        private sealed class RecordingEventSink : IMissionEventSink
        {
            private readonly TaskCompletionSource _retrying = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public ConcurrentQueue<MissionTaskEvent> Events { get; } = new();

            public ValueTask EmitAsync(MissionTaskEvent evt, CancellationToken cancellationToken = default)
            {
                Events.Enqueue(evt);
                if (evt.Type == MissionTaskEventType.Retrying)
                {
                    _retrying.TrySetResult();
                }
                return ValueTask.CompletedTask;
            }

            public Task WaitForAsync(MissionTaskEventType type, string taskId)
            {
                if (type == MissionTaskEventType.Retrying && Events.Any(evt => evt.Type == type && evt.TaskId == taskId))
                {
                    return Task.CompletedTask;
                }
                return _retrying.Task;
            }
        }

        private static class InterlockedExtensions
        {
            public static void Max(ref int location, int value)
            {
                int current;
                do
                {
                    current = Volatile.Read(ref location);
                    if (value <= current)
                    {
                        return;
                    }
                }
                while (Interlocked.CompareExchange(ref location, value, current) != current);
            }
        }
    }
}
