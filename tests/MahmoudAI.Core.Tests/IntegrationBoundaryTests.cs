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
            var guarded = new CapabilityGuardedMcpToolGateway(broker, gateway);
            var tool = new McpToolDescriptor("server", "read_file", "Read a file", "{}", "workspace/read");

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
