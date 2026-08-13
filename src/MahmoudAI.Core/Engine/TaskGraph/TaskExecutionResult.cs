using System;
using System.Collections.Generic;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public enum TaskExecutionStatus
    {
        Succeeded,
        Failed,
        TimedOut,
        Cancelled,
        SkippedDependencyFailure
    }

    public enum GraphExecutionStatus
    {
        Completed,
        Failed,
        Cancelled,
        Stalled
    }

    public sealed record TaskExecutionResult(
        string TaskId,
        TaskExecutionStatus Status,
        int Attempts,
        TimeSpan Duration,
        string? Error);

    public sealed record GraphExecutionResult(
        GraphExecutionStatus Status,
        IReadOnlyDictionary<string, TaskExecutionResult> Tasks,
        DateTimeOffset StartedAt,
        DateTimeOffset FinishedAt);
}
