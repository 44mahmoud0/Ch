using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Runtime
{
    public record StrategyRecord(string TaskType, string StrategyName, double SuccessRate, int ExecutionCount);

    public class ExperienceLearningEngine
    {
        private readonly ILogger<ExperienceLearningEngine> _logger;
        private readonly Dictionary<string, StrategyRecord> _strategies = new(StringComparer.OrdinalIgnoreCase);

        public ExperienceLearningEngine(ILogger<ExperienceLearningEngine> logger)
        {
            _logger = logger;
        }

        public void RecordOutcome(string taskType, string strategyName, bool success)
        {
            string key = $"{taskType}:{strategyName}";
            if (!_strategies.TryGetValue(key, out var record))
            {
                record = new StrategyRecord(taskType, strategyName, success ? 1.0 : 0.0, 1);
            }
            else
            {
                int newCount = record.ExecutionCount + 1;
                double newRate = ((record.SuccessRate * record.ExecutionCount) + (success ? 1.0 : 0.0)) / newCount;
                record = new StrategyRecord(taskType, strategyName, newRate, newCount);
            }
            _strategies[key] = record;
            _logger.LogInformation("Updated experience record for strategy {Strategy} under {TaskType}: Success Rate {Rate:P0} across {Count} runs", strategyName, taskType, record.SuccessRate, record.ExecutionCount);
        }

        public string SuggestBestStrategy(string taskType)
        {
            string bestStrategy = "default_strategy";
            double bestRate = -1.0;

            foreach (var kvp in _strategies)
            {
                if (kvp.Value.TaskType.Equals(taskType, StringComparison.OrdinalIgnoreCase) && kvp.Value.SuccessRate > bestRate)
                {
                    bestRate = kvp.Value.SuccessRate;
                    bestStrategy = kvp.Value.StrategyName;
                }
            }

            return bestStrategy;
        }
    }
}
