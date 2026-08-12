using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Runtime
{
    public record TelemetryEvent(string EventId, string MissionId, string Category, string Message, DateTime Timestamp);

    public class MissionTelemetryManager
    {
        private readonly ILogger<MissionTelemetryManager> _logger;
        private readonly ConcurrentBag<TelemetryEvent> _events = new();

        public MissionTelemetryManager(ILogger<MissionTelemetryManager> logger)
        {
            _logger = logger;
        }

        public void LogEvent(string missionId, string category, string message)
        {
            var telemetryEvent = new TelemetryEvent(Guid.NewGuid().ToString("N"), missionId, category, message, DateTime.UtcNow);
            _events.Add(telemetryEvent);
            _logger.LogInformation("[Telemetry] [{Category}] Mission {MissionId}: {Message}", category, missionId, message);
        }

        public IEnumerable<TelemetryEvent> GetEventsForMission(string missionId)
        {
            return _events.Where(e => e.MissionId.Equals(missionId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
