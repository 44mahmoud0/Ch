using System;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Automation;
using MahmoudAI.Core.Integration;
using MahmoudAI.Core.Security;
using MahmoudAI.WindowsIntegration.Automation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.WindowsIntegration.Tests
{
    public class WindowsAutomationIntegrationTests
    {
        [Fact]
        public void Composition_CreatesGuardedBackend_WithoutExposingRawTypes()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance);
            var backend = WindowsAutomationComposition.CreateGuardedBackend(broker, NullLoggerFactory.Instance);

            Assert.NotNull(backend);
            Assert.IsType<CapabilityGuardedAutomationBackend>(backend);
        }

        [Fact]
        public async Task Win32Backend_Inspect_InvalidHwnd_ReturnsFailure()
        {
            var backend = new Win32AutomationBackend(NullLogger<Win32AutomationBackend>.Instance);
            var request = new AutomationRequest(
                RequiredCapability: CapabilityType.ScreenCapture,
                Scope: "hwnd:99999999",
                Operation: AutomationOperation.Inspect,
                Target: "hwnd:99999999"
            );

            var result = await backend.ExecuteAsync(request, CancellationToken.None);
            Assert.False(result.Succeeded);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public async Task Uia3Backend_Inspect_InvalidTarget_ReturnsFailureGracefully()
        {
            using var backend = new Uia3AutomationBackend();
            var request = new AutomationRequest(
                RequiredCapability: CapabilityType.ScreenCapture,
                Scope: "hwnd:99999999",
                Operation: AutomationOperation.Inspect,
                Target: "hwnd:99999999"
            );

            var result = await backend.ExecuteAsync(request, CancellationToken.None);
            Assert.False(result.Succeeded);
        }
    }
}
