using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Security
{
    public enum CapabilityType
    {
        FilesRead,
        FilesWrite,
        FilesDelete,
        ScreenCapture,
        MouseControl,
        KeyboardControl,
        MicrophoneAccess,
        NetworkAccess,
        PluginExecution
    }

    public record CapabilityLease(
        string LeaseId,
        CapabilityType Capability,
        string Scope,
        DateTime GrantedAt,
        DateTime ExpiresAt,
        string GrantedBy
    );

    public sealed class CapabilityLeaseHandle : IDisposable
    {
        private readonly CancellationToken _revocationToken;

        internal CapabilityLeaseHandle(CapabilityLease lease, CancellationToken revocationToken)
        {
            Lease = lease;
            _revocationToken = revocationToken;
        }

        public CapabilityLease Lease { get; }
        public CancellationToken RevocationToken => _revocationToken;

        public void Dispose()
        {
            // A handle observes revocation; it does not revoke a shared lease on disposal.
        }
    }

    public class AdvancedPermissionBroker
    {
        private readonly ILogger<AdvancedPermissionBroker> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly ConcurrentDictionary<string, CapabilityLease> _activeLeases = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _leaseRevocations = new();
        public bool EmergencyStopTriggered { get; private set; }
        public bool SafeModeActive { get; private set; }

        private readonly IUserApprovalService? _approvalService;

        public Func<CapabilityType, string, CancellationToken, Task<bool>>? ApprovalDelegate { get; set; }

        public AdvancedPermissionBroker(
            ILogger<AdvancedPermissionBroker> logger,
            IUserApprovalService? approvalService = null,
            TimeProvider? timeProvider = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _approvalService = approvalService;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public void TriggerEmergencyStop()
        {
            EmergencyStopTriggered = true;
            SafeModeActive = true;
            RevokeAll();
            _logger.LogCritical("EMERGENCY STOP TRIGGERED! All capability leases revoked. Safe mode active.");
        }

        public void SetSafeMode(bool active)
        {
            SafeModeActive = active;
            _logger.LogWarning("Safe mode set to {Active}", active);
        }

        public void ResetEmergencyStop()
        {
            EmergencyStopTriggered = false;
            SafeModeActive = false;
            RevokeAll();
            _logger.LogWarning("Emergency stop reset by explicit user action.");
        }

        public async Task<bool> RequestCapabilityAsync(
            CapabilityType capability,
            string scope,
            TimeSpan duration,
            CancellationToken cancellationToken)
        {
            var handle = await RequestCapabilityLeaseAsync(capability, scope, duration, cancellationToken).ConfigureAwait(false);
            handle?.Dispose();
            return handle is not null;
        }

        public async Task<CapabilityLeaseHandle?> RequestCapabilityLeaseAsync(
            CapabilityType capability,
            string scope,
            TimeSpan duration,
            CancellationToken cancellationToken)
        {
            if (duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Capability lease duration must be positive.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (EmergencyStopTriggered || SafeModeActive)
            {
                _logger.LogWarning("Capability {Capability} denied because SafeMode/EmergencyStop is active.", capability);
                return null;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            RemoveExpiredLeases(now);

            foreach (var lease in _activeLeases.Values)
            {
                if (lease.Capability == capability && lease.ExpiresAt > now && ScopeMatches(lease.Scope, scope)
                    && _leaseRevocations.TryGetValue(lease.LeaseId, out var existingRevocation))
                {
                    return new CapabilityLeaseHandle(lease, existingRevocation.Token);
                }
            }

            var approved = await RequestApprovalAsync(capability, scope, cancellationToken).ConfigureAwait(false);
            if (!approved)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (EmergencyStopTriggered || SafeModeActive)
            {
                return null;
            }

            var grantedAt = _timeProvider.GetUtcNow().UtcDateTime;
            var newLease = new CapabilityLease(
                Guid.NewGuid().ToString("N"),
                capability,
                scope,
                grantedAt,
                grantedAt.Add(duration),
                "UserApproved");
            var revocationSource = new CancellationTokenSource();
            revocationSource.CancelAfter(duration);
            _activeLeases[newLease.LeaseId] = newLease;
            _leaseRevocations[newLease.LeaseId] = revocationSource;
            _logger.LogInformation(
                "Granted capability lease {LeaseId} for {Capability} on scope {Scope}",
                newLease.LeaseId,
                capability,
                scope);
            return new CapabilityLeaseHandle(newLease, revocationSource.Token);
        }

        public bool RevokeLease(string leaseId)
        {
            var removed = _activeLeases.TryRemove(leaseId, out _);
            if (_leaseRevocations.TryRemove(leaseId, out var revocationSource))
            {
                revocationSource.Cancel();
                revocationSource.Dispose();
                removed = true;
            }

            if (removed)
            {
                _logger.LogInformation("Capability lease {LeaseId} revoked.", leaseId);
            }

            return removed;
        }

        public void RevokeAll()
        {
            foreach (var leaseId in _activeLeases.Keys)
            {
                RevokeLease(leaseId);
            }

            foreach (var leaseId in _leaseRevocations.Keys)
            {
                RevokeLease(leaseId);
            }

            _logger.LogInformation("All capability leases manually revoked.");
        }

        private async Task<bool> RequestApprovalAsync(
            CapabilityType capability,
            string scope,
            CancellationToken cancellationToken)
        {
            try
            {
                if (_approvalService is not null)
                {
                    return await _approvalService.RequestApprovalAsync(capability, scope, cancellationToken).ConfigureAwait(false);
                }

                if (ApprovalDelegate is not null)
                {
                    return await ApprovalDelegate(capability, scope, cancellationToken).ConfigureAwait(false);
                }

                _logger.LogWarning("No approval provider configured.");
                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Approval failed for {Capability}.", capability);
                return false;
            }
        }

        private void RemoveExpiredLeases(DateTime now)
        {
            foreach (var pair in _activeLeases)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    RevokeLease(pair.Key);
                }
            }
        }

        private static bool ScopeMatches(string grantedScope, string requestedScope)
        {
            return grantedScope == "*" || grantedScope.Equals(requestedScope, StringComparison.OrdinalIgnoreCase);
        }
    }
}
