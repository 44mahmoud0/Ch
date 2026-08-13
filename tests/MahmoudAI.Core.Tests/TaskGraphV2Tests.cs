using System;
using System.Collections.Generic;
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
            var events = new List<MissionTaskEvent>();
            var scheduler = new TaskGraphScheduler(null, ev => events.Add(ev));

            var definitions = new List<MissionTaskDefinition>
            {
                new("t1", "Task 1", Array.Empty<string>(), async ct => { await Task.Delay(20, ct); return true; }, TimeSpan.FromSeconds(2), new RetryPolicy(1, TimeSpan.Zero, 1, TimeSpan.Zero, false)),
                new("t2", "Task 2", Array.Empty<string>(), async ct => { await Task.Delay(20, ct); return true; }, TimeSpan.FromSeconds(2), new RetryPolicy(1, TimeSpan.Zero, 1, TimeSpan.Zero, false)),
                new("t3", "Task 3", new[] { "t1", "t2" }, async ct => { await Task.Delay(10, ct); return true; }, TimeSpan.FromSeconds(2), new RetryPolicy(1, TimeSpan.Zero, 1, TimeSpan.Zero, false)),
            };

            var result = await SchedulerExecuteWrapper(scheduler, "m1", definitions, 2);

            result.Status.Should().Be(GraphExecutionStatus.Completed);
            result.Tasks.Count.Should().Be(3);
            result.Tasks["t1"].Status.Should().Be(TaskExecutionStatus.Succeeded);
            result.Tasks["t2"].Status.Should().Be(TaskExecutionStatus.Succeeded);
            result.Tasks["t3"].Status.Should().Be(TaskExecutionStatus.Succeeded);
        }

        [Fact]
        public async Task Scheduler_ShouldDetectCyclesAndThrow()
        {
            var scheduler = new TaskGraphScheduler();
            var definitions = new List<MissionTaskDefinition>
            {
                new("t1", "Task 1", new[] { "t2" }, _ => Task.FromResult(true), TimeSpan.FromSeconds(1), new RetryPolicy(1, TimeSpan.Zero, 1, TimeSpan.Zero, false)),
                new("t2", "Task 2", new[] { "t1" }, _ => Task.FromResult(true), TimeSpan.FromSeconds(1), new RetryPolicy(1, TimeSpan.Zero, 1, TimeSpan.Zero, false)),
            };

            Func<Task> act = async () => await scheduler.ExecuteGraphAsync("m1", definitions);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Scheduler_ShouldPropagateDependencyFailureAsSkipped()
        {
            var scheduler = new TaskGraphScheduler();
            var definitions = new List<MissionTaskDefinition>
            {
                new("t1", "Failing Task", Array.Empty<string>(), _ => Task.FromResult(false), TimeSpan.FromSeconds(1), new RetryPolicy(1, TimeSpan.Zero, 1, TimeSpan.Zero, false)),
                new("t2", "Dependent Task", new[] { "t1" }, _ => Task.FromResult(true), TimeSpan.FromSeconds(1), new RetryPolicy(1, TimeSpan.Zero, 1, TimeSpan.Zero, false)),
            };

            var result = await SchedulerExecuteWrapper(scheduler, "m1", definitions, 2);

            result.Status.Should().Be(GraphExecutionStatus.Failed);
            result.Tasks["t1"].Status.Should().Be(TaskExecutionStatus.Failed);
            result.Tasks["t2"].Status.Should().Be(TaskExecutionStatus.SkippedDependencyFailure);
        }

        [Fact]
        public async Task Scheduler_ShouldRespectRetryAndTimeout()
        {
            int attempts = 0;
            var policy = new RetryPolicy(3, TimeSpan.FromMilliseconds(10), 2, TimeSpan.FromMilliseconds(50), false);
            var definition = new MissionTaskDefinition(
                "retrying",
                "Retrying Task",
                Array.Empty<string>(),
                ct =>
                {
                    attempts++;
                    if (attempts < 3) return Task.FromResult(false);
                    return Task.FromResult(true);
                },
                TimeSpan.FromSeconds(1),
                policy);

            var scheduler = new TaskGraphScheduler();
            var result = await SchedulerExecuteWrapper(scheduler, "m1", new[] { definition }, 1);

            result.Status.Should().Be(GraphExecutionStatus.Completed);
            result.Tasks["retrying"].Status.Should().Be(TaskExecutionStatus.Succeeded);
            result.Tasks["retrying"].Attempts.Should().Be(3);
        }

        private static async Task<GraphExecutionResult> SchedulerExecuteWrapper(TaskGraphScheduler scheduler, string missionId, IEnumerable<MissionTaskDefinition> defs, int maxConcurrency)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await scheduler.ExecuteGraphAsync(missionId, defs, maxConcurrency, cts.Token);
        }
    }
}
