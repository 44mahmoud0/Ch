using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public sealed record RetryPolicy
    {
        public RetryPolicy(
            int maxAttempts,
            TimeSpan initialDelay,
            double backoffFactor,
            TimeSpan maxDelay,
            bool useJitter)
        {
            if (maxAttempts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "MaxAttempts must be at least 1.");
            }
            if (initialDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(initialDelay), initialDelay, "InitialDelay cannot be negative.");
            }
            if (maxDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDelay), maxDelay, "MaxDelay cannot be negative.");
            }
            if (double.IsNaN(backoffFactor) || double.IsInfinity(backoffFactor) || backoffFactor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(backoffFactor), backoffFactor, "BackoffFactor must be finite and greater than zero.");
            }

            MaxAttempts = maxAttempts;
            InitialDelay = initialDelay;
            BackoffFactor = backoffFactor;
            MaxDelay = maxDelay;
            UseJitter = useJitter;
        }

        public int MaxAttempts { get; }
        public TimeSpan InitialDelay { get; }
        public double BackoffFactor { get; }
        public TimeSpan MaxDelay { get; }
        public bool UseJitter { get; }
    }

    public sealed record MissionTaskDefinition
    {
        public MissionTaskDefinition(
            string id,
            string name,
            IReadOnlyList<string> dependencies,
            Func<CancellationToken, Task<bool>> executeAsync,
            TimeSpan timeout,
            RetryPolicy retry)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Task ID cannot be null or whitespace.", nameof(id));
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Task name cannot be null or whitespace.", nameof(name));
            }
            ArgumentNullException.ThrowIfNull(dependencies);
            ArgumentNullException.ThrowIfNull(executeAsync);
            ArgumentNullException.ThrowIfNull(retry);
            if (timeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout cannot be negative.");
            }

            Id = id;
            Name = name;
            Dependencies = Array.AsReadOnly(dependencies.ToArray());
            ExecuteAsync = executeAsync;
            Timeout = timeout;
            Retry = retry;
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> Dependencies { get; }
        public Func<CancellationToken, Task<bool>> ExecuteAsync { get; }
        public TimeSpan Timeout { get; }
        public RetryPolicy Retry { get; }
    }
}
