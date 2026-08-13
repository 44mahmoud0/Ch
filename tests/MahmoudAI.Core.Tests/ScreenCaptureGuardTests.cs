using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Automation;
using MahmoudAI.Core.Integration;
using MahmoudAI.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public sealed class ScreenCaptureGuardTests
    {
        [Fact]
        public async Task DeniedCapture_DoesNotReachBackend()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (_, _, _) => Task.FromResult(false)
            };
            var fake = new RecordingCaptureBackend();
            using var guarded = new CapabilityGuardedScreenCaptureBackend(broker, fake);

            var result = await guarded.CaptureAsync(CreateRequest(), CancellationToken.None);

            Assert.Equal(ScreenCaptureStatus.Denied, result.Status);
            Assert.False(fake.Called);
        }

        [Fact]
        public async Task ApprovedCapture_ReachesBackendWithWindowScope()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (_, _, _) => Task.FromResult(true)
            };
            var fake = new RecordingCaptureBackend();
            using var guarded = new CapabilityGuardedScreenCaptureBackend(broker, fake);

            var result = await guarded.CaptureAsync(CreateRequest(), CancellationToken.None);

            Assert.Equal(ScreenCaptureStatus.Captured, result.Status);
            Assert.True(fake.Called);
        }

        [Fact]
        public async Task CancelledCapture_ReturnsCancelledWithoutReachingBackend()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (_, _, _) => Task.FromResult(true)
            };
            var fake = new RecordingCaptureBackend();
            using var guarded = new CapabilityGuardedScreenCaptureBackend(broker, fake);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var result = await guarded.CaptureAsync(CreateRequest(), cancellation.Token);

            Assert.Equal(ScreenCaptureStatus.Cancelled, result.Status);
            Assert.False(fake.Called);
        }

        [Fact]
        public async Task EmergencyStop_DeniesCaptureBeforeBackend()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (_, _, _) => Task.FromResult(true)
            };
            broker.TriggerEmergencyStop();
            var fake = new RecordingCaptureBackend();
            using var guarded = new CapabilityGuardedScreenCaptureBackend(broker, fake);

            var result = await guarded.CaptureAsync(CreateRequest(), CancellationToken.None);

            Assert.Equal(ScreenCaptureStatus.Denied, result.Status);
            Assert.False(fake.Called);
        }

        private static ScreenCaptureRequest CreateRequest()
        {
            return new ScreenCaptureRequest(
                new ScreenCaptureTarget(
                    ScreenCaptureTargetKind.Window,
                    Hwnd: (nint)123,
                    ProcessId: 42));
        }

        private sealed class RecordingCaptureBackend : IScreenCaptureBackend
        {
            public bool Called { get; private set; }

            public Task<CapturedScreenFrame> CaptureAsync(
                ScreenCaptureRequest request,
                CancellationToken cancellationToken)
            {
                Called = true;
                return Task.FromResult(new CapturedScreenFrame(
                    ScreenCaptureStatus.Captured,
                    new ScreenFrameMetadata(
                        "test-frame",
                        DateTimeOffset.UtcNow,
                        1,
                        1,
                        4,
                        1.0f,
                        1.0f,
                        0,
                        0,
                        request.Target.ProcessId ?? 0,
                        request.Target.Hwnd ?? nint.Zero),
                    new byte[4]));
            }
        }
    }
}
