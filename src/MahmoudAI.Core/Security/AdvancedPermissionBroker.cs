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

        public Func<CapabilityType, string, Task<bool>>? ApprovalDelegate { get; set; }

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

        public bool RequestCapability(CapabilityType capability, string scope, TimeSpan duration)
        {
            if (EmergencyStopTriggered || SafeModeActive)
            {
                _logger.LogWarning("Capability request {Capability} denied due to EmergencyStop or SafeMode.", capability);
                return false;
            }

            // Clean expired leases
            foreach (var kvp in _activeLeases)
            {
                if (kvp.Value.ExpiresAt < DateTime.UtcNow)
                {
                    _activeLeases.TryRemove(kvp.Key, out _);
                }
            }

            // Check if active lease covers this scope
            foreach (var lease in _activeLeases.Values)
            {
                if (lease.Capability == capability && (lease.Scope == "*" || lease.Scope.Equals(scope, StringComparison.OrdinalIgnoreCase)))
                {
                    if (lease.ExpiresAt > DateTime.UtcNow)
                    {
                        _logger.LogInformation("Capability lease matched for {Capability} on scope {Scope}", capability, scope);
                        return true;
                    }
                }
            }

            // If approval delegate is registered, prompt interactively
            bool approved = false;
            if (ApprovalDelegate != null)
            {
                try
                {
                    approved = ApprovalDelegate(capability, scope).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during interactive capability approval for {Capability}", capability);
                    approved = false;
                }
            }
            else
            {
                _logger.LogWarning("No approval delegate registered. Denying capability request {Capability} on scope {Scope}", capability, scope);
                return false;
            }

            if (!approved)
            {
                _logger.LogWarning("User or policy denied capability request {Capability} on scope {Scope}", capability, scope);
                return false;
            }

            var leaseId = Guid.NewGuid().ToString("N");
            var newLease = new CapabilityLease(leaseId, capability, scope, DateTime.UtcNow, DateTime.UtcNow.Add(duration), "UserApproved");
            _activeLeases[leaseId] = newLease;
            _logger.LogInformation("Granted capability lease {LeaseId} for {Capability} on scope {Scope} for {Duration}", leaseId, capability, scope, duration);
            return true;
        }

        public void RevokeAll()
        {
            _activeLeases.Clear();
            _logger.LogInformation("All capability leases manually revoked.");
        }
    }
}
