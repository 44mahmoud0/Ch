using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MahmoudAI.Core.Integration;
using MahmoudAI.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public sealed class IntegrationBoundaryTests
    {
        [Fact]
        public async Task AutomationDecorator_ShouldNotInvokeBackendWhenCapabilityIsDenied()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (_, _, _) => Task.FromResult(false)
            };
            var backend = new RecordingAutomationBackend();
            var guarded = new CapabilityGuardedAutomationBackend(broker, backend);
            var request = new AutomationRequest(
                CapabilityType.MouseControl,
                "window:calculator",
                AutomationOperation.Pointer,
                "button:add");

            var result = await guarded.ExecuteAsync(request, CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            backend.CallCount.Should().Be(0);
        }

        [Fact]
        public async Task AutomationDecorator_ShouldAuthorizeBeforeInvokingBackend()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (_, _, _) => Task.FromResult(true)
            };
            var backend = new RecordingAutomationBackend();
            var guarded = new CapabilityGuardedAutomationBackend(broker, backend);
            var request = new AutomationRequest(
                CapabilityType.KeyboardControl,
                "window:notepad",
                AutomationOperation.Keyboard,
                "text-input",
                "hello");

            var result = await guarded.ExecuteAsync(request, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            backend.CallCount.Should().Be(1);
        }

        [Fact]
        public void ManifestPolicy_ShouldRejectUnknownToolIdentity()
        {
            var policy = new ManifestMcpToolPolicy(new[]
            {
                new TrustedMcpToolManifest("trusted", "server", "read_file", CapabilityType.PluginExecution, "workspace/read")
            });
            var untrusted = new McpToolCallRequest(
                new McpToolDescriptor("server", "delete_file", "Delete a file", "{}", "*", "trusted"),
                "{}",
                CapabilityType.FilesDelete);

            var decision = policy.Authorize(untrusted);

            decision.Allowed.Should().BeFalse();
            decision.Reason.Should().Contain("No trusted MCP manifest");
        }

        [Fact]
        public void RiskPolicy_ShouldBlockPhysicalInputForGameContext()
        {
            var policy = new ConservativeAutomationRiskPolicy();
            var request = new AutomationRequest(
                CapabilityType.KeyboardControl,
                "process:game",
                AutomationOperation.Keyboard,
                "foreground",
                "ordinary text",
                new AutomationContext(TargetProcessName: "game.exe", IsGame: true));

            var allowed = policy.IsAllowed(request, out var reason);

            allowed.Should().BeFalse();
            reason.Should().Contain("game targets");
        }

        [Fact]
        public async Task McpDecorator_ShouldAlwaysRequestPluginExecution()
        {
            CapabilityType? approvedCapability = null;
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (capability, _, _) =>
                {
                    approvedCapability = capability;
                    return Task.FromResult(true);
                }
            };
            var gateway = new RecordingMcpGateway();
            var tool = new McpToolDescriptor("server", "read_file", "Read a file", "{}", "workspace/read");
            var policy = new ManifestMcpToolPolicy(new[]
            {
                new TrustedMcpToolManifest("", "server", "read_file", CapabilityType.PluginExecution, "workspace/read")
            });
            var guarded = new CapabilityGuardedMcpToolGateway(broker, gateway, policy);

            var result = await guarded.CallToolAsync(
                new McpToolCallRequest(tool, "{}", CapabilityType.FilesRead),
                CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            approvedCapability.Should().Be(CapabilityType.PluginExecution);
            gateway.CallCount.Should().Be(1);
        }

        private sealed class RecordingAutomationBackend : IWindowsAutomationBackend
        {
            public int CallCount { get; private set; }

            public Task<AutomationResult> ExecuteAsync(AutomationRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return Task.FromResult(new AutomationResult(true, "ok"));
            }
        }

        private sealed class RecordingMcpGateway : IMcpToolGateway
        {
            public int CallCount { get; private set; }

            public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<McpToolDescriptor>>(Array.Empty<McpToolDescriptor>());
            }

            public Task<McpToolCallResult> CallToolAsync(McpToolCallRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return Task.FromResult(new McpToolCallResult(true, "{}"));
            }
        }
    }
}
