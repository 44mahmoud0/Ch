using System;
using System.Collections.Generic;

namespace MahmoudAI.Core.Automation
{
    public enum FusionStatus
    {
        Matched,
        Ambiguous,
        NoMatch,
        StaleObservation,
        ProcessMismatch,
        InvalidCoordinateTransform,
        ProviderError,
        Cancelled
    }

    public sealed record FusionScoreBreakdown(
        double GeometryScore,
        double TextSimilarityScore,
        double ControlTypeCompatibilityScore,
        double SemanticPriorityScore,
        double TotalScore,
        double UiaTextScore,
        double OcrTextScore);

    public sealed record FusionCandidate(
        string ElementId,
        string ControlType,
        string ElementName,
        ScreenRect ElementBounds,
        OcrLine? MatchedOcrLine,
        string MatchedText,
        FusionScoreBreakdown ScoreBreakdown,
        nint SourceHwnd,
        int SourceProcessId,
        string FrameId,
        DateTimeOffset CapturedAt,
        string OcrEngine,
        string? RecognizedLanguage,
        bool IsAmbiguous,
        bool OcrTextCorroborated);

    public sealed record ScreenFusionResult(
        FusionStatus Status,
        IReadOnlyList<FusionCandidate> Candidates,
        FusionCandidate? BestCandidate,
        string? Error = null);

    public sealed record ScreenObservation(
        nint Hwnd,
        int ProcessId,
        DateTimeOffset CapturedAt,
        ScreenFrameMetadata FrameMetadata,
        FrameCoordinateTransform Transform,
        OcrResult OcrResult,
        IReadOnlyList<UiaElementSnapshot> UiaElements,
        TimeSpan MaxFreshnessWindow);
}
