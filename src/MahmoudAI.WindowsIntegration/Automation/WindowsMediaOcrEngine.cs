using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Automation;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MahmoudAI.WindowsIntegration.Automation
{
    [SupportedOSPlatform("windows10.0.17763.0")]
    public sealed class WindowsMediaOcrEngine : IOcrEngine
    {
        private readonly ILogger<WindowsMediaOcrEngine> _logger;

        public WindowsMediaOcrEngine(ILogger<WindowsMediaOcrEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MahmoudAI.Core.Automation.OcrResult> RecognizeAsync(
            RedactedScreenFrame frame,
            OcrRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(frame);
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            if (!frame.Succeeded || frame.PixelBuffer is null || frame.Metadata is null)
            {
                return new MahmoudAI.Core.Automation.OcrResult(
                    OcrStatus.EmptyImage,
                    "WindowsMediaOcr",
                    null,
                    Array.Empty<MahmoudAI.Core.Automation.OcrLine>(),
                    string.Empty,
                    frame.Error ?? "Frame capture failed or frame is empty.");
            }

            if (frame.PixelBuffer.Length == 0)
            {
                return new MahmoudAI.Core.Automation.OcrResult(
                    OcrStatus.EmptyImage,
                    "WindowsMediaOcr",
                    null,
                    Array.Empty<MahmoudAI.Core.Automation.OcrLine>(),
                    string.Empty,
                    "Redacted frame pixel buffer is empty.");
            }

            Windows.Media.Ocr.OcrEngine? engine = null;
            string? resolvedLangTag = null;

            try
            {
                var availableLanguages = Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages;
                var langTag = ResolveLanguageTag(request.Language, availableLanguages);

                if (langTag is null)
                {
                    return new MahmoudAI.Core.Automation.OcrResult(
                        OcrStatus.LanguageUnavailable,
                        "WindowsMediaOcr",
                        request.Language.ToString(),
                        Array.Empty<MahmoudAI.Core.Automation.OcrLine>(),
                        string.Empty,
                        $"Requested OCR language '{request.Language}' is not installed on this Windows installation.");
                }

                var language = new Windows.Globalization.Language(langTag);
                engine = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(language);
                resolvedLangTag = langTag;

                if (engine is null)
                {
                    return new MahmoudAI.Core.Automation.OcrResult(
                        OcrStatus.LanguageUnavailable,
                        "WindowsMediaOcr",
                        langTag,
                        Array.Empty<MahmoudAI.Core.Automation.OcrLine>(),
                        string.Empty,
                        $"Could not instantiate Windows.Media.OcrEngine for language '{langTag}'.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve or create Windows.Media.OcrEngine.");
                return new MahmoudAI.Core.Automation.OcrResult(
                    OcrStatus.ProviderError,
                    "WindowsMediaOcr",
                    null,
                    Array.Empty<MahmoudAI.Core.Automation.OcrLine>(),
                    string.Empty,
                    $"OCR engine initialization error: {ex.Message}");
            }

            try
            {
                using var softwareBitmap = SoftwareBitmap.CreateCopyFromBuffer(
                    frame.PixelBuffer.AsBuffer(),
                    BitmapPixelFormat.Bgra8,
                    frame.Metadata.PixelWidth,
                    frame.Metadata.PixelHeight,
                    BitmapAlphaMode.Premultiplied);

                cancellationToken.ThrowIfCancellationRequested();
                var ocrResult = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken).ConfigureAwait(false);

                if (ocrResult is null)
                {
                    return new MahmoudAI.Core.Automation.OcrResult(
                        OcrStatus.Success,
                        "WindowsMediaOcr",
                        resolvedLangTag,
                        Array.Empty<MahmoudAI.Core.Automation.OcrLine>(),
                        string.Empty,
                        null);
                }

                var lines = new List<MahmoudAI.Core.Automation.OcrLine>();
                var fullTextBuilder = new System.Text.StringBuilder();

                var originX = frame.Metadata.ScreenOriginX;
                var originY = frame.Metadata.ScreenOriginY;
                var dpiScaleX = frame.Metadata.DpiScaleX > 0 ? frame.Metadata.DpiScaleX : 1.0f;
                var dpiScaleY = frame.Metadata.DpiScaleY > 0 ? frame.Metadata.DpiScaleY : 1.0f;

                foreach (var line in ocrResult.Lines)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var words = new List<MahmoudAI.Core.Automation.OcrWord>();

                    foreach (var word in line.Words)
                    {
                        var rect = word.BoundingRect;
                        var wBounds = CreatePolygon(rect.X, rect.Y, rect.Width, rect.Height, originX, originY, dpiScaleX, dpiScaleY);
                        words.Add(new MahmoudAI.Core.Automation.OcrWord(word.Text, wBounds, null));
                    }

                    double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                    foreach (var word in line.Words)
                    {
                        var r = word.BoundingRect;
                        minX = Math.Min(minX, r.X);
                        minY = Math.Min(minY, r.Y);
                        maxX = Math.Max(maxX, r.X + r.Width);
                        maxY = Math.Max(maxY, r.Y + r.Height);
                    }
                    if (minX == double.MaxValue) { minX = 0; minY = 0; maxX = 100; maxY = 20; }
                    var lBounds = CreatePolygon(minX, minY, maxX - minX, maxY - minY, originX, originY, dpiScaleX, dpiScaleY);
                    lines.Add(new MahmoudAI.Core.Automation.OcrLine(line.Text, words, lBounds));
                    fullTextBuilder.AppendLine(line.Text);
                }

                return new MahmoudAI.Core.Automation.OcrResult(
                    OcrStatus.Success,
                    "WindowsMediaOcr",
                    resolvedLangTag,
                    lines,
                    fullTextBuilder.ToString().TrimEnd(),
                    null);
            }
            catch (OperationCanceledException)
            {
                return new MahmoudAI.Core.Automation.OcrResult(
                    OcrStatus.Cancelled,
                    "WindowsMediaOcr",
                    resolvedLangTag,
                    Array.Empty<MahmoudAI.Core.Automation.OcrLine>(),
                    string.Empty,
                    "OCR recognition was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR recognition failed during execution.");
                return new MahmoudAI.Core.Automation.OcrResult(
                    OcrStatus.ProviderError,
                    "WindowsMediaOcr",
                    resolvedLangTag,
                    Array.Empty<MahmoudAI.Core.Automation.OcrLine>(),
                    string.Empty,
                    $"OCR recognition error: {ex.Message}");
            }
        }

        private static string? ResolveLanguageTag(OcrLanguageMode mode, IReadOnlyList<Windows.Globalization.Language> availableLanguages)
        {
            var availableTags = new List<string>();
            foreach (var lang in availableLanguages)
            {
                availableTags.Add(lang.LanguageTag);
            }

            switch (mode)
            {
                case OcrLanguageMode.Arabic:
                    foreach (var tag in availableTags)
                    {
                        if (tag.StartsWith("ar", StringComparison.OrdinalIgnoreCase)) return tag;
                    }
                    return null;

                case OcrLanguageMode.English:
                    foreach (var tag in availableTags)
                    {
                        if (tag.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return tag;
                    }
                    return null;

                case OcrLanguageMode.Auto:
                default:
                    foreach (var tag in availableTags)
                    {
                        if (tag.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return tag;
                    }
                    return availableTags.Count > 0 ? availableTags[0] : null;
            }
        }

        private static ScreenPolygon CreatePolygon(
            double x,
            double y,
            double width,
            double height,
            int originX,
            int originY,
            float dpiScaleX,
            float dpiScaleY)
        {
            var fx = (float)x;
            var fy = (float)y;
            var fw = (float)width;
            var fh = (float)height;

            var absX = originX + (fx / dpiScaleX);
            var absY = originY + (fy / dpiScaleY);
            var absW = fw / dpiScaleX;
            var absH = fh / dpiScaleY;

            return new ScreenPolygon(
                TopLeft: new ScreenPoint(fx, fy),
                TopRight: new ScreenPoint(fx + fw, fy),
                BottomRight: new ScreenPoint(fx + fw, fy + fh),
                BottomLeft: new ScreenPoint(fx, fy + fh),
                AbsoluteTopLeft: new ScreenPoint(absX, absY),
                AbsoluteBottomRight: new ScreenPoint(absX + absW, absY + absH));
        }
    }
}
