using System;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Integration;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Automation
{
    public class WindowsAutomationEngine
    {
        private readonly IWindowsAutomationBackend _backend;
        private readonly ILogger<WindowsAutomationEngine> _logger;

        public WindowsAutomationEngine(IWindowsAutomationBackend backend, ILogger<WindowsAutomationEngine> logger)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> ClickAtCoordinatesAsync(int x, int y, CancellationToken cancellationToken)
        {
            var request = new AutomationRequest(
                MahmoudAI.Core.Security.CapabilityType.MouseControl,
                $"desktop:click:{x},{y}",
                AutomationOperation.Pointer,
                $"{x},{y}");
            var result = await _backend.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Mouse click failed: {Error}", result.Error);
            }

            return result.Succeeded;
        }

        public async Task<bool> SendTextAsync(string text, CancellationToken cancellationToken)
        {
            if (text.Contains("cheat", StringComparison.OrdinalIgnoreCase)
                || text.Contains("bypass", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Gaming safety policy triggered: blocked restricted input pattern.");
                return false;
            }

            var request = new AutomationRequest(
                MahmoudAI.Core.Security.CapabilityType.KeyboardControl,
                "desktop:keyboard",
                AutomationOperation.Keyboard,
                "foreground",
                text);
            var result = await _backend.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Keyboard input failed: {Error}", result.Error);
            }

            return result.Succeeded;
        }
    }
}
