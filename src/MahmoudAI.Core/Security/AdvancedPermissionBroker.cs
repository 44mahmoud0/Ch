using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    public class AdvancedPermissionBroker
    {
        private readonly ILogger<AdvancedPermissionBroker> _logger;
        private readonly ConcurrentDictionary<string, CapabilityLease> _activeLeases = new();
        public bool EmergencyStopTriggered { get; private set; } = false;
        public bool SafeModeActive { get; private set; } = false;

        public Func<CapabilityType, string, CancellationToken, Task<bool>>? ApprovalDelegate { get; set; }

        public AdvancedPermissionBroker(ILogger<AdvancedPermissionBroker> logger)
        {
            _logger = logger;
        }

        public void TriggerEmergencyStop()
        {
            EmergencyStopTriggered = true;
            SafeModeActive = true;
            _activeLeases.Clear();
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
            _activeLeases.Clear();
            _logger.LogWarning("Emergency stop reset by explicit user action.");
        }

        public bool RequestCapability(CapabilityType capability, string scope, TimeSpan duration)
        {
            return RequestCapabilityAsync(capability, scope, duration, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task<bool> RequestCapabilityAsync(CapabilityType capability, string scope, TimeSpan duration, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (EmergencyStopTriggered || SafeModeActive)
            {
                _logger.LogWarning("Capability {Capability} denied because SafeMode/EmergencyStop is active.", capability);
                return false;
            }

            DateTime now = DateTime.UtcNow;

            foreach (var pair in _activeLeases)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _activeLeases.TryRemove(pair.Key, out _);
                }
            }

            foreach (var lease in _activeLeases.Values)
            {
                if (lease.Capability == capability && lease.ExpiresAt > now && ScopeMatches(lease.Scope, scope))
                {
                    return true;
                }
            }

            if (ApprovalDelegate is null)
            {
                _logger.LogWarning("No approval provider configured.");
                return false;
            }

            bool approved;
            try
            {
                approved = await ApprovalDelegate(capability, scope, ct);
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

            if (!approved) return false;

            ct.ThrowIfCancellationRequested();

            var newLease = new CapabilityLease(
                Guid.NewGuid().ToString("N"),
                capability,
                scope,
                now,
                now.Add(duration),
                "UserApproved"
            );

            _activeLeases[newLease.LeaseId] = newLease;
            _logger.LogInformation("Granted capability lease {LeaseId} for {Capability} on scope {Scope}", newLease.LeaseId, capability, scope);
            return true;
        }

        private static bool ScopeMatches(string grantedScope, string requestedScope)
        {
            return grantedScope == "*" || grantedScope.Equals(requestedScope, StringComparison.OrdinalIgnoreCase);
        }

        public void RevokeAll()
        {
            _activeLeases.Clear();
            _logger.LogInformation("All capability leases manually revoked.");
        }
    }
}
