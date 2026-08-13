using System;
using MahmoudAI.Core.Security;

namespace MahmoudAI.Core.Integration
{
    public sealed class ConservativeAutomationRiskPolicy : IAutomationRiskPolicy
    {
        public bool IsAllowed(AutomationRequest request, out string? reason)
        {
            ArgumentNullException.ThrowIfNull(request);
            var context = request.Context;

            if (context?.IsGame == true
                && request.Operation is AutomationOperation.Pointer or AutomationOperation.Keyboard)
            {
                reason = "Physical input automation is disabled for game targets.";
                return false;
            }

            if (context?.IsSensitive == true
                && request.Operation is AutomationOperation.Pointer
                    or AutomationOperation.Keyboard
                    or AutomationOperation.SetValue)
            {
                reason = "Physical or value-changing automation is disabled for sensitive targets.";
                return false;
            }

            var expectedCapability = request.Operation switch
            {
                AutomationOperation.Pointer => CapabilityType.MouseControl,
                AutomationOperation.Keyboard => CapabilityType.KeyboardControl,
                AutomationOperation.Capture => CapabilityType.ScreenCapture,
                AutomationOperation.Inspect => request.RequiredCapability == CapabilityType.UiAutomationRead ? CapabilityType.UiAutomationRead : request.RequiredCapability,
                AutomationOperation.Activate or AutomationOperation.SetValue => request.RequiredCapability == CapabilityType.UiAutomationInteract ? CapabilityType.UiAutomationInteract : request.RequiredCapability,
                _ => request.RequiredCapability
            };
            if (request.RequiredCapability != expectedCapability)
            {
                reason = $"Capability {request.RequiredCapability} does not authorize {request.Operation}.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
