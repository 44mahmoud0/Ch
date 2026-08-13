using System;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Automation;
using MahmoudAI.Core.Security;
using MahmoudAI.WindowsIntegration.Automation;
using Xunit;

namespace MahmoudAI.WindowsIntegration.Tests
{
    public class WindowsAutomationIntegrationTests
    {
        [Fact]
        public void Composition_CreatesGuardedBackend_WithoutExposingRawTypes()
        {
            var backend = WindowsAutomationComposition.CreateGuardedBackend(TimeProvider.System);
            Assert.NotNull(backend);
            Assert.IsType<CapabilityGuardedAutomationBackend>(backend);
        }

        [Fact]
        public async Task Win32Backend_Inspect_InvalidHwnd_ReturnsFailure()
        {
            var backend = new Win32AutomationBackend(TimeProvider.System);
            var request = new AutomationRequest(
                Operation: AutomationOperation.Inspect,
                Target: "hwnd:99999999",
                Text: null,
                Coordinates: null,
                RequiredCapability: CapabilityType.ScreenCapture
            );

            var result = await backend.ExecuteAsync(request, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Contains("invalid", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Uia3Backend_Inspect_InvalidTarget_ReturnsFailureGracefully()
        {
            var backend = new Uia3AutomationBackend(TimeProvider.System);
            var request = new AutomationRequest(
                Operation: AutomationOperation.Inspect,
                Target: "hwnd:99999999",
                Text: null,
                Coordinates: null,
                RequiredCapability: CapabilityType.ScreenCapture
            );

            var result = await backend.ExecuteAsync(request, CancellationToken.None);
            Assert.False(result.Success);
        }
    }
}
