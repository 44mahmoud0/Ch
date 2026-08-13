using System;
using MahmoudAI.Core.Integration;
using MahmoudAI.Core.Security;
using Microsoft.Extensions.Logging;
using MahmoudAI.WindowsIntegration.Automation;

namespace MahmoudAI.Core.Automation
{
    public static class WindowsAutomationComposition
    {
        public static IWindowsAutomationBackend CreateGuardedBackend(
            AdvancedPermissionBroker permissionBroker,
            ILoggerFactory loggerFactory,
            IAutomationRiskPolicy? riskPolicy = null,
            TimeSpan? leaseDuration = null)
        {
            ArgumentNullException.ThrowIfNull(permissionBroker);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            var rawBackend = new Win32AutomationBackend(
                loggerFactory.CreateLogger<Win32AutomationBackend>());
            var semanticBackend = new SemanticFirstAutomationBackend(
                new Uia3AutomationBackend(),
                rawBackend);
            return new CapabilityGuardedAutomationBackend(
                permissionBroker,
                semanticBackend,
                leaseDuration,
                riskPolicy);
        }
    }
}
