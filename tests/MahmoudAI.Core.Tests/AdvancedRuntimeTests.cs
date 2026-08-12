using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MahmoudAI.Core.Engine;
using MahmoudAI.Core.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public class AdvancedRuntimeTests
    {
        [Fact]
        public async Task MissionRecoveryEngine_ShouldSaveAndLoadCheckpoints()
        {
            var recovery = new MissionRecoveryEngine(NullLogger<MissionRecoveryEngine>.Instance);
            await recovery.SaveCheckpointAsync("m_100", 2, new { status = "running" }, CancellationToken.None);

            var checkpoint = await recovery.LoadCheckpointAsync("m_100", CancellationToken.None);
            checkpoint.Should().NotBeNull();
            checkpoint!.StepIndex.Should().Be(2);
        }

        [Fact]
        public void ExperienceLearningEngine_ShouldRecommendBestStrategy()
        {
            var learning = new ExperienceLearningEngine(NullLogger<ExperienceLearningEngine>.Instance);
            learning.RecordOutcome("coding", "strategy_a", false);
            learning.RecordOutcome("coding", "strategy_b", true);
            learning.RecordOutcome("coding", "strategy_b", true);

            string best = learning.SuggestBestStrategy("coding");
            best.Should().Be("strategy_b");
        }
    }
}
