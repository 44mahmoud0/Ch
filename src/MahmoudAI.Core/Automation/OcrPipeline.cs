using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Automation
{
    public sealed class OcrPipeline
    {
        private readonly IOcrEngine _primaryEngine;
        private readonly IOcrEngine? _fallbackEngine;
        private readonly ILogger<OcrPipeline> _logger;

        public OcrPipeline(
            IOcrEngine primaryEngine,
            IOcrEngine? fallbackEngine,
            ILogger<OcrPipeline> logger)
        {
            _primaryEngine = primaryEngine ?? throw new ArgumentNullException(nameof(primaryEngine));
            _fallbackEngine = fallbackEngine;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OcrResult> RecognizeAsync(
            RedactedScreenFrame frame,
            OcrRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(frame);
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            if (!frame.Succeeded || frame.PixelBuffer is null || frame.Metadata is null)
            {
                _logger.LogWarning("OcrPipeline received invalid or failed redacted screen frame.");
                return new OcrResult(
                    OcrStatus.EmptyImage,
                    "OcrPipeline",
                    null,
                    Array.Empty<OcrLine>(),
                    string.Empty,
                    frame.Error ?? "Redacted frame is invalid or failed.");
            }

            if (frame.PixelBuffer.Length == 0)
            {
                return new OcrResult(
                    OcrStatus.EmptyImage,
                    "OcrPipeline",
                    null,
                    Array.Empty<OcrLine>(),
                    string.Empty,
                    "Redacted pixel buffer is empty.");
            }

            var result = await _primaryEngine.RecognizeAsync(frame, request, cancellationToken).ConfigureAwait(false);
            if (result.Status == OcrStatus.Success || result.Status == OcrStatus.EmptyImage)
            {
                return result;
            }

            if (_fallbackEngine is not null && (result.Status == OcrStatus.LanguageUnavailable || result.Status == OcrStatus.ProviderError))
            {
                _logger.LogWarning("Primary OCR engine failed with status {Status}; falling back to secondary engine.", result.Status);
                return await _fallbackEngine.RecognizeAsync(frame, request, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
    }
}
