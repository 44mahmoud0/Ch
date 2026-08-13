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
        public void Composition_CreatesGuardedSemanticAutomation()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance);
            var semantic = WindowsAutomationComposition.CreateGuardedSemanticAutomation(broker, NullLoggerFactory.Instance);
            try
            {
                Assert.NotNull(semantic);
                Assert.IsType<CapabilityGuardedUiaSemanticAutomation>(semantic);
            }
            finally
            {
                ((IDisposable)semantic).Dispose();
            }
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
        public async Task Uia3Backend_Query_InvalidWindow_ReturnsNotFound()
        {
            using var backend = new Uia3AutomationBackend();
            var query = new UiaQueryRequest(
                "hwnd:99999999",
                new UiaSelectorPath(new UiaSelector(AutomationId: "SaveButton")));

            var result = await backend.QueryAsync(query, CancellationToken.None);

            Assert.Equal(UiaMatchStatus.NotFound, result.Status);
            Assert.Empty(result.Candidates);
        }

        [Fact]
        public async Task Uia3Backend_Action_InvalidWindow_ReturnsNotFound()
        {
            using var backend = new Uia3AutomationBackend();
            var action = new UiaActionRequest(
                new UiaQueryRequest(
                    "hwnd:99999999",
                    new UiaSelectorPath(new UiaSelector(Name: "Save", ControlType: UiaControlKind.Button))),
                UiaActionType.Invoke);

            var result = await backend.ExecuteAsync(action, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(UiaMatchStatus.NotFound, result.Status);
        }

        [Fact]
        public async Task Uia3Backend_CancelledQuery_ReturnsCancelled()
        {
            using var backend = new Uia3AutomationBackend();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var query = new UiaQueryRequest(
                "hwnd:99999999",
                new UiaSelectorPath(new UiaSelector(Name: "Save")));

            var result = await backend.QueryAsync(query, cancellation.Token);

            Assert.Equal(UiaMatchStatus.Cancelled, result.Status);
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
