using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Runtime
{
    public enum ModelTier
    {
        FastLocal,
        BalancedCloud,
        AdvancedReasoning
    }

    public class ModelRouter
    {
        private readonly ILogger<ModelRouter> _logger;

        public ModelRouter(ILogger<ModelRouter> logger)
        {
            _logger = logger;
        }

        public string SelectModel(ModelTier tier, bool isOffline)
        {
            if (isOffline)
            {
                return "llama3:local";
            }

            return tier switch
            {
                ModelTier.FastLocal => "llama3:8b",
                ModelTier.BalancedCloud => "gpt-4o-mini",
                ModelTier.AdvancedReasoning => "claude-3-5-sonnet",
                _ => "gpt-4o-mini"
            };
        }
    }

    public class HealthMonitor
    {
        private readonly ILogger<HealthMonitor> _logger;
        public string LocalAiStatus { get; set; } = "Healthy";
        public string DatabaseStatus { get; set; } = "Healthy";
        public string McpBridgeStatus { get; set; } = "Healthy";

        public HealthMonitor(ILogger<HealthMonitor> logger)
        {
            _logger = logger;
        }

        public Dictionary<string, string> CheckAllServices()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "LocalAI", LocalAiStatus },
                { "Database", DatabaseStatus },
                { "McpBridge", McpBridgeStatus }
            };
        }
    }
}
