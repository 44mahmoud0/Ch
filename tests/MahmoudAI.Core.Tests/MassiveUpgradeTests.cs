using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MahmoudAI.Core.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public class MassiveUpgradeTests
    {
        [Fact]
        public void MissionTelemetryManager_ShouldLogAndRetrieveEvents()
        {
            var telemetry = new MissionTelemetryManager(NullLogger<MissionTelemetryManager>.Instance);
            telemetry.LogEvent("m_99", "Execution", "Step 1 started");
            telemetry.LogEvent("m_99", "Execution", "Step 1 completed");

            var events = telemetry.GetEventsForMission("m_99");
            events.Should().HaveCount(2);
        }

        [Fact]
        public async Task ModelBenchmarkCenter_ShouldBenchmarkModel()
        {
            var client = new AiProviderClient(NullLogger<AiProviderClient>.Instance);
            var benchmark = new ModelBenchmarkCenter(client, NullLogger<ModelBenchmarkCenter>.Instance);

            var result = await benchmark.BenchmarkModelAsync("llama3", "http://localhost:11434", CancellationToken.None);
            result.Should().NotBeNull();
            result.ModelName.Should().Be("llama3");
        }
    }
}
