using System;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public enum MissionTaskEventType
    {
        Queued,
        Started,
        Retrying,
        Succeeded,
        Failed,
        TimedOut,
        Cancelled,
        Skipped
    }

    public sealed record MissionTaskEvent(
        string MissionId,
        string TaskId,
        MissionTaskEventType Type,
        DateTimeOffset Timestamp,
        int Attempt,
        string? Message);
}
