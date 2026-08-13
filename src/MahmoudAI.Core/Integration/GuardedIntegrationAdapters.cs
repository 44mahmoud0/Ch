using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Automation;
using MahmoudAI.Core.Security;

namespace MahmoudAI.Core.Integration
{
    public sealed class CapabilityGuardedAutomationBackend : IWindowsAutomationBackend, IDisposable
    {
        private readonly AdvancedPermissionBroker _permissionBroker;
        private readonly IWindowsAutomationBackend _inner;
        private readonly IAutomationRiskPolicy _riskPolicy;
        private readonly TimeSpan _leaseDuration;

        public CapabilityGuardedAutomationBackend(
            AdvancedPermissionBroker permissionBroker,
            IWindowsAutomationBackend inner,
            TimeSpan? leaseDuration = null,
            IAutomationRiskPolicy? riskPolicy = null)
        {
            _permissionBroker = permissionBroker ?? throw new ArgumentNullException(nameof(permissionBroker));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _riskPolicy = riskPolicy ?? new ConservativeAutomationRiskPolicy();
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
            if (!_riskPolicy.IsAllowed(request, out var riskReason))
            {
                return new AutomationResult(false, null, riskReason);
            }

            var lease = await _permissionBroker.RequestCapabilityLeaseAsync(
                request.RequiredCapability,
                request.Scope,
                _leaseDuration,
                cancellationToken).ConfigureAwait(false);
            if (lease is null)
            {
                return new AutomationResult(false, null, "Capability denied by policy or user.");
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lease.RevocationToken);
            return await _inner.ExecuteAsync(request, linkedCancellation.Token).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_inner is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public sealed class CapabilityGuardedUiaSemanticAutomation : IUiaSemanticAutomation, IDisposable
    {
        private readonly AdvancedPermissionBroker _permissionBroker;
        private readonly IUiaSemanticAutomation _inner;
        private readonly IAutomationRiskPolicy _riskPolicy;
        private readonly TimeSpan _leaseDuration;

        public CapabilityGuardedUiaSemanticAutomation(
            AdvancedPermissionBroker permissionBroker,
            IUiaSemanticAutomation inner,
            TimeSpan? leaseDuration = null,
            IAutomationRiskPolicy? riskPolicy = null)
        {
            _permissionBroker = permissionBroker ?? throw new ArgumentNullException(nameof(permissionBroker));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _riskPolicy = riskPolicy ?? new ConservativeAutomationRiskPolicy();
            _leaseDuration = leaseDuration ?? TimeSpan.FromMinutes(5);
            if (_leaseDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(leaseDuration), "The capability lease duration must be positive.");
            }
        }

        public async Task<UiaQueryResult> QueryAsync(
            UiaQueryRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var automationRequest = CreateAutomationRequest(
                request.WindowTarget,
                AutomationOperation.Inspect,
                CapabilityType.UiAutomationRead,
                request.TargetProcessId);
            if (!_riskPolicy.IsAllowed(automationRequest, out var riskReason))
            {
                return new UiaQueryResult(UiaMatchStatus.Denied, Array.Empty<UiaElementSnapshot>(), riskReason);
            }

            var lease = await _permissionBroker.RequestCapabilityLeaseAsync(
                automationRequest.RequiredCapability,
                automationRequest.Scope,
                _leaseDuration,
                cancellationToken).ConfigureAwait(false);
            if (lease is null)
            {
                return new UiaQueryResult(UiaMatchStatus.Denied, Array.Empty<UiaElementSnapshot>(), "UI automation read capability denied by policy or user.");
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lease.RevocationToken);
            return await _inner.QueryAsync(request, linkedCancellation.Token).ConfigureAwait(false);
        }

        public async Task<UiaActionResult> ExecuteAsync(
            UiaActionRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var capability = CapabilityType.UiAutomationInteract;
            var operation = request.Action == UiaActionType.SetValue
                ? AutomationOperation.SetValue
                : AutomationOperation.Activate;
            var automationRequest = CreateAutomationRequest(
                request.Query.WindowTarget,
                operation,
                capability,
                request.Query.TargetProcessId);
            if (!_riskPolicy.IsAllowed(automationRequest, out var riskReason))
            {
                return new UiaActionResult(false, UiaMatchStatus.Denied, riskReason);
            }

            var lease = await _permissionBroker.RequestCapabilityLeaseAsync(
                automationRequest.RequiredCapability,
                automationRequest.Scope,
                _leaseDuration,
                cancellationToken).ConfigureAwait(false);
            if (lease is null)
            {
                return new UiaActionResult(false, UiaMatchStatus.Denied, "UIA action capability denied by policy or user.");
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lease.RevocationToken);
            return await _inner.ExecuteAsync(request, linkedCancellation.Token).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_inner is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private static AutomationRequest CreateAutomationRequest(
            string windowTarget,
            AutomationOperation operation,
            CapabilityType capability,
            int? processId)
        {
            return new AutomationRequest(
                capability,
                $"uia:{operation}:{windowTarget}",
                operation,
                windowTarget,
                Context: new AutomationContext(TargetProcessId: processId));
        }
    }

    public sealed class CapabilityGuardedMcpToolGateway : IMcpToolGateway
    {
        private readonly AdvancedPermissionBroker _permissionBroker;
        private readonly IMcpToolGateway _inner;
        private readonly IMcpToolPolicy _policy;
        private readonly TimeSpan _leaseDuration;

        public CapabilityGuardedMcpToolGateway(
            AdvancedPermissionBroker permissionBroker,
            IMcpToolGateway inner,
            IMcpToolPolicy policy,
            TimeSpan? leaseDuration = null)
        {
            _permissionBroker = permissionBroker ?? throw new ArgumentNullException(nameof(permissionBroker));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
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
            var decision = _policy.Authorize(request);
            if (!decision.Allowed)
            {
                return new McpToolCallResult(false, "{}", decision.Reason ?? "MCP tool denied by policy.");
            }

            var lease = await _permissionBroker.RequestCapabilityLeaseAsync(
                decision.Capability,
                decision.Scope,
                _leaseDuration,
                cancellationToken).ConfigureAwait(false);
            if (lease is null)
            {
                return new McpToolCallResult(false, "{}", "MCP capability denied by policy or user.");
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lease.RevocationToken);
            return await _inner.CallToolAsync(request, linkedCancellation.Token).ConfigureAwait(false);
        }
    }
}
