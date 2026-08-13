using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public sealed record RetryPolicy(
        int MaxAttempts,
        TimeSpan InitialDelay,
        double BackoffFactor,
        TimeSpan MaxDelay,
        bool UseJitter);

    public sealed record MissionTaskDefinition(
        string Id,
        string Name,
        IReadOnlyList<string> Dependencies,
        Func<CancellationToken, Task<bool>> ExecuteAsync,
        TimeSpan Timeout,
        RetryPolicy Retry);
}
