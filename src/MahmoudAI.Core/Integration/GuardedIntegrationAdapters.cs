using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Security;

namespace MahmoudAI.Core.Integration
{
    public sealed class CapabilityGuardedAutomationBackend : IWindowsAutomationBackend
    {
        private readonly AdvancedPermissionBroker _permissionBroker;
        private readonly IWindowsAutomationBackend _inner;
        private readonly TimeSpan _leaseDuration;

        public CapabilityGuardedAutomationBackend(
            AdvancedPermissionBroker permissionBroker,
            IWindowsAutomationBackend inner,
            TimeSpan? leaseDuration = null)
        {
            _permissionBroker = permissionBroker ?? throw new ArgumentNullException(nameof(permissionBroker));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _leaseDuration = leaseDuration ?? TimeSpan.FromMinutes(5);
            if (_leaseDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(leaseDuration), "The capability lease duration must be positive.");
            }
        }

        public async Task<AutomationResult> ExecuteAsync(
            AutomationRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var granted = await _permissionBroker.RequestCapabilityAsync(
                request.RequiredCapability,
                request.Scope,
                _leaseDuration,
                cancellationToken).ConfigureAwait(false);
            if (!granted)
            {
                return new AutomationResult(false, null, "Capability denied by policy or user.");
            }

            return await _inner.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    public sealed class CapabilityGuardedMcpToolGateway : IMcpToolGateway
    {
        private readonly AdvancedPermissionBroker _permissionBroker;
        private readonly IMcpToolGateway _inner;
        private readonly TimeSpan _leaseDuration;

        public CapabilityGuardedMcpToolGateway(
            AdvancedPermissionBroker permissionBroker,
            IMcpToolGateway inner,
            TimeSpan? leaseDuration = null)
        {
            _permissionBroker = permissionBroker ?? throw new ArgumentNullException(nameof(permissionBroker));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _leaseDuration = leaseDuration ?? TimeSpan.FromMinutes(5);
            if (_leaseDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(leaseDuration), "The capability lease duration must be positive.");
            }
        }

        public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken)
        {
            return _inner.ListToolsAsync(cancellationToken);
        }

        public async Task<McpToolCallResult> CallToolAsync(
            McpToolCallRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Tool);
            var granted = await _permissionBroker.RequestCapabilityAsync(
                CapabilityType.PluginExecution,
                request.Tool.RequiredScope,
                _leaseDuration,
                cancellationToken).ConfigureAwait(false);
            if (!granted)
            {
                return new McpToolCallResult(false, "{}", "PluginExecution capability denied by policy or user.");
            }

            return await _inner.CallToolAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
