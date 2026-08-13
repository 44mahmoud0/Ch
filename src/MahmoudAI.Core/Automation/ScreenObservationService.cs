using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Automation
{
    public interface IScreenObservationService
    {
        Task<ScreenObservationResult> ObserveAndFuseAsync(
            ScreenCaptureRequest captureRequest,
            ScreenPrivacyContext privacyContext,
            OcrRequest ocrRequest,
            string targetQuery,
            CancellationToken cancellationToken);
    }

    public sealed record ScreenObservationResult(
        ScreenObservation? Observation,
        ScreenFusionResult FusionResult);

    public sealed class ScreenObservationService : IScreenObservationService
    {
        private readonly IScreenCaptureBackend _captureBackend;
        private readonly IScreenPrivacyFilter _privacyFilter;
        private readonly OcrPipeline _ocrPipeline;
        private readonly IUiaSemanticAutomation _uiaAutomation;
        private readonly ScreenFusionEngine _fusionEngine;
        private readonly ILogger<ScreenObservationService> _logger;

        public ScreenObservationService(
            IScreenCaptureBackend captureBackend,
            IScreenPrivacyFilter privacyFilter,
            OcrPipeline ocrPipeline,
            IUiaSemanticAutomation uiaAutomation,
            ScreenFusionEngine fusionEngine,
            ILogger<ScreenObservationService> logger)
        {
            _captureBackend = captureBackend ?? throw new ArgumentNullException(nameof(captureBackend));
            _privacyFilter = privacyFilter ?? throw new ArgumentNullException(nameof(privacyFilter));
            _ocrPipeline = ocrPipeline ?? throw new ArgumentNullException(nameof(ocrPipeline));
            _uiaAutomation = uiaAutomation ?? throw new ArgumentNullException(nameof(uiaAutomation));
            _fusionEngine = fusionEngine ?? throw new ArgumentNullException(nameof(fusionEngine));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ScreenObservationResult> ObserveAndFuseAsync(
            ScreenCaptureRequest captureRequest,
            ScreenPrivacyContext privacyContext,
            OcrRequest ocrRequest,
            string targetQuery,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(captureRequest);
            ArgumentNullException.ThrowIfNull(privacyContext);
            ArgumentNullException.ThrowIfNull(ocrRequest);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetQuery);
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Guarded Capture
            using var capturedFrame = await _captureBackend.CaptureAsync(captureRequest, cancellationToken).ConfigureAwait(false);
            if (!capturedFrame.Succeeded || capturedFrame.Metadata is null)
            {
                var failedFusion = new ScreenFusionResult(FusionStatus.ProviderError, Array.Empty<FusionCandidate>(), null, capturedFrame.Error ?? "Capture failed.");
                return new ScreenObservationResult(null, failedFusion);
            }

            // Verify transform is present and authoritative
            if (capturedFrame.Transform is null)
            {
                _logger.LogWarning("Captured frame is missing authoritative transform.");
                var transformFailure = new ScreenFusionResult(FusionStatus.InvalidCoordinateTransform, Array.Empty<FusionCandidate>(), null, "Captured frame is missing authoritative coordinate transform.");
                return new ScreenObservationResult(null, transformFailure);
            }

            // 2. Privacy Filter / Redaction (Pixel buffers are scoped and disposed securely here)
            using var redactedFrame = await _privacyFilter.RedactAsync(capturedFrame, privacyContext, cancellationToken).ConfigureAwait(false);

            // 3. OCR Pipeline
            var ocrResult = await _ocrPipeline.RecognizeAsync(redactedFrame, ocrRequest, cancellationToken).ConfigureAwait(false);

            // 4. Fresh UIA Snapshot via semantic automation query request
            var hwnd = captureRequest.Target.Hwnd ?? nint.Zero;
            var uiaQuery = new UiaQueryRequest(
                WindowTarget: hwnd != nint.Zero ? $"hwnd:{hwnd}" : "active",
                Path: new UiaSelectorPath(new UiaSelector(Name: targetQuery)),
                TargetProcessId: captureRequest.Target.ProcessId);
            var uiaResult = await _uiaAutomation.QueryAsync(uiaQuery, cancellationToken).ConfigureAwait(false);
            var elements = uiaResult.Candidates;

            // 5. Screen Observation Construction (Pixel-free immutable semantic snapshot)
            var observation = new ScreenObservation(
                hwnd,
                capturedFrame.Metadata.SourceProcessId,
                capturedFrame.Metadata.CapturedAt,
                capturedFrame.Metadata,
                capturedFrame.Transform,
                ocrResult,
                elements,
                TimeSpan.FromSeconds(5));

            // 6. Fusion
            var fusionResult = _fusionEngine.Fuse(observation, targetQuery);

            return new ScreenObservationResult(observation, fusionResult);
        }
    }
}
