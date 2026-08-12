using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MahmoudAI.Core.Automation;
using MahmoudAI.Core.Persona;
using MahmoudAI.Core.Runtime;
using MahmoudAI.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public class ExpandedFeatureTests
    {
        [Fact]
        public void PermissionBroker_ShouldEnforceSafetyAndEmergencyStop()
        {
            var broker = new PermissionBroker(NullLogger<PermissionBroker>.Instance);
            broker.RequestPermission(PermissionType.FileRead).Should().BeTrue();

            broker.TriggerEmergencyStop();
            broker.RequestPermission(PermissionType.FileRead).Should().BeFalse();
        }

        [Fact]
        public void PersonaStateMachine_ShouldTransitionStates()
        {
            var persona = new PersonaStateMachine(NullLogger<PersonaStateMachine>.Instance);
            persona.CurrentState.Should().Be(PersonaState.Idle);

            persona.TransitionTo(PersonaState.Thinking);
            persona.CurrentState.Should().Be(PersonaState.Thinking);
        }

        [Fact]
        public async Task WindowsAutomationEngine_ShouldBlockUnsafeGamingInputs()
        {
            var broker = new PermissionBroker(NullLogger<PermissionBroker>.Instance);
            var engine = new WindowsAutomationEngine(broker, NullLogger<WindowsAutomationEngine>.Instance);

            bool success = await engine.SendTextAsync("enable cheat mode", CancellationToken.None);
            success.Should().BeFalse();
        }

        [Fact]
        public void ModelRouter_ShouldSelectCorrectTier()
        {
            var router = new ModelRouter(NullLogger<ModelRouter>.Instance);
            router.SelectModel(ModelTier.FastLocal, true).Should().Be("llama3:local");
            router.SelectModel(ModelTier.AdvancedReasoning, false).Should().Be("claude-3-5-sonnet");
        }
    }
}
