using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Automation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public sealed class ClosedLoopVerifierTests
    {
        [Fact]
        public async Task ClosedLoopVerifier_SucceedsWhenPreActionValidAndExpectationSatisfied()
        {
            var now = DateTimeOffset.UtcNow;
            var ticket = new TargetIdentityTicket(
                (nint)456, 123, 1000, "hwnd:456",
                new UiaSelectorPath(new UiaSelector(Name: "Save")),
                "btnSave", "Save", "Button",
                new ScreenRect(10, 10, 40, 20), "f1", now);

            var metadata = new ScreenFrameMetadata("f2", now, 100, 100, 400, 1.0f, 1.0f, 0, 0, 123, (nint)456);
            var transform = new FrameCoordinateTransform(
                new ScreenRect(0, 0, 800, 600), new ScreenRect(0, 0, 100, 100), 100, 100, 1.0, 1.0, CoordinateSpace.AbsoluteDesktopPhysicalPixels);
            var ocrResult = new OcrResult(OcrStatus.Success, "TestEngine", "en", Array.Empty<OcrLine>(), string.Empty);
            var uiaElements = new[]
            {
                new UiaElementSnapshot("btnSave", "Save", "Button", "ButtonClass", "Win32", 123, 10, 10, 40, 20, true, false, Array.Empty<string>())
            };

            var observation = new ScreenObservation((nint)456, 123, now, metadata, transform, ocrResult, uiaElements, TimeSpan.FromSeconds(5));
            var fusionResult = new ScreenFusionResult(FusionStatus.Matched, new[]
            {
                new FusionCandidate("btnSave", "Button", "Save", new ScreenRect(10, 10, 40, 20), null, "Save",
                    new FusionScoreBreakdown(1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 0.0), (nint)456, 123, "f2", now, "TestEngine", "en", false, false)
            }, null);

            var stubObsService = new VerifierStubObservationService(new ScreenObservationResult(observation, fusionResult));
            var uiaAutomation = new VerifierStubUiaAutomation();
            var broker = new Security.AdvancedPermissionBroker(NullLogger<Security.AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (cap, scope, ct) => Task.FromResult(true)
            };

            var verifier = new ClosedLoopVerifier(stubObsService, uiaAutomation, broker, NullLogger<ClosedLoopVerifier>.Instance);

            var expectation = new VerificationExpectation(ExpectationType.ElementExists);
            var captureRequest = new ScreenCaptureRequest(new ScreenCaptureTarget(ScreenCaptureTargetKind.Window, Hwnd: (nint)456, ProcessId: 123));
            var privacyContext = new ScreenPrivacyContext(ScreenPrivacySensitivity.Public, false, false, false);
            var ocrRequest = new OcrRequest(OcrLanguageMode.English);

            bool actionExecuted = false;
            Func<CancellationToken, Task> action = ct =>
            {
                actionExecuted = true;
                return Task.CompletedTask;
            };

            var result = await verifier.ExecuteAndVerifyAsync(
                ticket, expectation, action, captureRequest, privacyContext, ocrRequest, CancellationToken.None);

            Assert.Equal(VerificationStatus.Verified, result.Status);
            Assert.True(actionExecuted);
        }

        [Fact]
        public async Task ClosedLoopVerifier_RejectsStaleTicketWithoutExecutingAction()
        {
            var past = DateTimeOffset.UtcNow.AddSeconds(-10);
            var ticket = new TargetIdentityTicket(
                (nint)456, 123, 1000, "hwnd:456",
                new UiaSelectorPath(new UiaSelector(Name: "Save")),
                "btnSave", "Save", "Button",
                new ScreenRect(10, 10, 40, 20), "f1", past);

            var stubObsService = new VerifierStubObservationService(null);
            var uiaAutomation = new VerifierStubUiaAutomation();
            var broker = new Security.AdvancedPermissionBroker(NullLogger<Security.AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (cap, scope, ct) => Task.FromResult(true)
            };

            var verifier = new ClosedLoopVerifier(stubObsService, uiaAutomation, broker, NullLogger<ClosedLoopVerifier>.Instance);

            var expectation = new VerificationExpectation(ExpectationType.ElementExists);
            var captureRequest = new ScreenCaptureRequest(new ScreenCaptureTarget(ScreenCaptureTargetKind.Window, Hwnd: (nint)456, ProcessId: 123));
            var privacyContext = new ScreenPrivacyContext(ScreenPrivacySensitivity.Public, false, false, false);
            var ocrRequest = new OcrRequest(OcrLanguageMode.English);

            bool actionExecuted = false;
            Func<CancellationToken, Task> action = ct =>
            {
                actionExecuted = true;
                return Task.CompletedTask;
            };

            var result = await verifier.ExecuteAndVerifyAsync(
                ticket, expectation, action, captureRequest, privacyContext, ocrRequest, CancellationToken.None);

            Assert.Equal(VerificationStatus.StaleObservation, result.Status);
            Assert.False(actionExecuted);
        }
    }

    internal sealed class VerifierStubObservationService : IScreenObservationService
    {
        private readonly ScreenObservationResult _result;

        public VerifierStubObservationService(ScreenObservationResult? result)
        {
            _result = result ?? new ScreenObservationResult(null, new ScreenFusionResult(FusionStatus.NoMatch, Array.Empty<FusionCandidate>(), null, "No match"));
        }

        public Task<ScreenObservationResult> ObserveAndFuseAsync(
            ScreenCaptureRequest captureRequest,
            ScreenPrivacyContext privacyContext,
            OcrRequest ocrRequest,
            string targetQuery,
            CancellationToken cancellationToken)
        {
            if (_result.Observation != null)
            {
                var engine = new ScreenFusionEngine(NullLogger<ScreenFusionEngine>.Instance);
                var fusion = engine.Fuse(_result.Observation, targetQuery);
                return Task.FromResult(new ScreenObservationResult(_result.Observation, fusion));
            }
            return Task.FromResult(_result);
        }
    }

    internal sealed class VerifierStubUiaAutomation : IUiaSemanticAutomation
    {
        public Task<UiaQueryResult> QueryAsync(UiaQueryRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new UiaQueryResult(UiaMatchStatus.Found, Array.Empty<UiaElementSnapshot>()));
        }

        public Task<UiaActionResult> ExecuteAsync(UiaActionRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new UiaActionResult(true, UiaMatchStatus.Found));
        }
    }
}
