using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Security
{
    public enum PermissionType
    {
        FileRead,
        FileWrite,
        FileDelete,
        ScreenCapture,
        MouseControl,
        KeyboardControl,
        Microphone,
        Network,
        Plugins
    }

    public class PermissionBroker
    {
        private readonly ILogger<PermissionBroker> _logger;
        private readonly HashSet<PermissionType> _grantedPermissions = new();
        public bool EmergencyStopTriggered { get; private set; } = false;
        public bool SafeModeActive { get; private set; } = false;

        public PermissionBroker(ILogger<PermissionBroker> logger)
        {
            _logger = logger;
            // Default safe permissions
            _grantedPermissions.Add(PermissionType.FileRead);
            _grantedPermissions.Add(PermissionType.Network);
        }

        public bool RequestPermission(PermissionType permission)
        {
            if (EmergencyStopTriggered || SafeModeActive)
            {
                _logger.LogWarning("Permission {Permission} denied due to Emergency Stop or Safe Mode active.", permission);
                return false;
            }

            bool granted = _grantedPermissions.Contains(permission);
            _logger.LogInformation("Permission {Permission} requested. Granted: {Granted}", permission, granted);
            return granted;
        }

        public void TriggerEmergencyStop()
        {
            EmergencyStopTriggered = true;
            SafeModeActive = true;
            _logger.LogError("EMERGENCY STOP TRIGGERED! All automation halted and Safe Mode activated.");
        }

        public void ResetEmergencyStop()
        {
            EmergencyStopTriggered = false;
            _logger.LogInformation("Emergency stop cleared. System returning to normal.");
        }
    }
}
