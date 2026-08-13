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
        ScreenObservation Observation,
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
                var failedOcr = new OcrResult(OcrStatus.ProviderError, "None", ocrRequest.Language.ToString(), Array.Empty<OcrLine>(), string.Empty, capturedFrame.Error ?? "Capture failed.");
                var failedTransform = new FrameCoordinateTransform(new ScreenRect(0, 0, 800, 600), new ScreenRect(0, 0, 100, 100), 100, 100, 1.0, 1.0, CoordinateSpace.AbsoluteDesktopPhysicalPixels);
                var emptyObs = new ScreenObservation(captureRequest.Target.Hwnd ?? nint.Zero, captureRequest.Target.ProcessId ?? 0, DateTimeOffset.UtcNow, new RedactedScreenFrame(capturedFrame.Status, capturedFrame.Metadata, capturedFrame.PixelBuffer, 0, capturedFrame.Transform, capturedFrame.Error), failedOcr, Array.Empty<UiaElementSnapshot>(), failedTransform, TimeSpan.FromSeconds(5));
                var failedFusion = new ScreenFusionResult(FusionStatus.ProviderError, Array.Empty<FusionCandidate>(), null, capturedFrame.Error ?? "Capture failed.");
                return new ScreenObservationResult(emptyObs, failedFusion);
            }

            // 2. Privacy Filter / Redaction
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

            // 5. Authoritative Transform
            var transform = redactedFrame.Transform ?? new FrameCoordinateTransform(
                new ScreenRect(capturedFrame.Metadata.ScreenOriginX, capturedFrame.Metadata.ScreenOriginY, capturedFrame.Metadata.PixelWidth, capturedFrame.Metadata.PixelHeight),
                new ScreenRect(0, 0, capturedFrame.Metadata.PixelWidth, capturedFrame.Metadata.PixelHeight),
                capturedFrame.Metadata.PixelWidth,
                capturedFrame.Metadata.PixelHeight,
                1.0,
                1.0,
                CoordinateSpace.AbsoluteDesktopPhysicalPixels);

            // 6. Screen Observation Construction
            var observation = new ScreenObservation(
                hwnd,
                capturedFrame.Metadata.SourceProcessId,
                capturedFrame.Metadata.CapturedAt,
                redactedFrame,
                ocrResult,
                elements,
                transform,
                TimeSpan.FromSeconds(5));

            // 7. Fusion
            var fusionResult = _fusionEngine.Fuse(observation, targetQuery);

            return new ScreenObservationResult(observation, fusionResult);
        }
    }
}
