using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Engine
{
    public record MissionCheckpoint(string MissionId, int StepIndex, string StateJson, DateTime Timestamp);

    public class MissionRecoveryEngine
    {
        private readonly ILogger<MissionRecoveryEngine> _logger;
        private readonly Dictionary<string, MissionCheckpoint> _checkpoints = new();

        public MissionRecoveryEngine(ILogger<MissionRecoveryEngine> logger)
        {
            _logger = logger;
        }

        public Task SaveCheckpointAsync(string missionId, int stepIndex, object state, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(state);
            var checkpoint = new MissionCheckpoint(missionId, stepIndex, json, DateTime.UtcNow);
            _checkpoints[missionId] = checkpoint;
            _logger.LogInformation("Saved checkpoint for mission {MissionId} at step {StepIndex}", missionId, stepIndex);
            return Task.CompletedTask;
        }

        public Task<MissionCheckpoint?> LoadCheckpointAsync(string missionId, CancellationToken ct)
        {
            _checkpoints.TryGetValue(missionId, out var checkpoint);
            if (checkpoint != null)
            {
                _logger.LogInformation("Recovered checkpoint for mission {MissionId} at step {StepIndex}", missionId, checkpoint.StepIndex);
            }
            return Task.FromResult(checkpoint);
        }
    }
}
