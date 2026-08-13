using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MahmoudAI.Core.Automation
{
    public enum OcrLanguageMode
    {
        Auto,
        Arabic,
        English
    }

    public enum OcrStatus
    {
        Success,
        LanguageUnavailable,
        ProviderError,
        Cancelled,
        EmptyImage
    }

    public sealed record ScreenPoint(
        float X,
        float Y);

    public sealed record ScreenPolygon(
        ScreenPoint TopLeft,
        ScreenPoint TopRight,
        ScreenPoint BottomRight,
        ScreenPoint BottomLeft,
        ScreenPoint AbsoluteTopLeft,
        ScreenPoint AbsoluteBottomRight);

    public sealed record OcrRequest(
        OcrLanguageMode Language = OcrLanguageMode.Auto,
        float MinimumConfidence = 0.0f);

    public sealed record OcrWord(
        string Text,
        ScreenPolygon Bounds,
        float? Confidence);

    public sealed record OcrLine(
        string Text,
        IReadOnlyList<OcrWord> Words,
        ScreenPolygon Bounds);

    public sealed record OcrResult(
        OcrStatus Status,
        string Engine,
        string? RecognizedLanguage,
        IReadOnlyList<OcrLine> Lines,
        string FullText,
        string? Error = null);

    public interface IOcrEngine
    {
        Task<OcrResult> RecognizeAsync(
            RedactedScreenFrame frame,
            OcrRequest request,
            CancellationToken cancellationToken);
    }
}
