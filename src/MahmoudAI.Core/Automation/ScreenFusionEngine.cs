using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Automation
{
    public sealed class ScreenFusionEngine
    {
        private readonly ILogger<ScreenFusionEngine> _logger;

        public ScreenFusionEngine(ILogger<ScreenFusionEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ScreenFusionResult Fuse(ScreenObservation observation, string targetQuery)
        {
            ArgumentNullException.ThrowIfNull(observation);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetQuery);

            var now = DateTimeOffset.UtcNow;
            if (now - observation.Timestamp > observation.MaxFreshnessWindow)
            {
                _logger.LogWarning("Screen observation is stale (age: {Age}s). Rejecting fusion.", (now - observation.Timestamp).TotalSeconds);
                return new ScreenFusionResult(FusionStatus.StaleObservation, Array.Empty<FusionCandidate>(), null, "Observation exceeds freshness window.");
            }

            if (!observation.Frame.Succeeded || observation.OcrResult.Status != OcrStatus.Success)
            {
                return new ScreenFusionResult(FusionStatus.ProviderError, Array.Empty<FusionCandidate>(), null, "Observation frame or OCR result failed.");
            }

            // Identity & Process Mismatch guard
            foreach (var element in observation.UiaElements)
            {
                if (observation.ProcessId > 0 && element.ProcessId > 0 && element.ProcessId != observation.ProcessId)
                {
                    _logger.LogWarning("Process mismatch detected between observation PID {ObsPid} and UIA element PID {ElementPid}.", observation.ProcessId, element.ProcessId);
                    return new ScreenFusionResult(FusionStatus.ProcessMismatch, Array.Empty<FusionCandidate>(), null, $"Process mismatch: observation PID {observation.ProcessId} vs element PID {element.ProcessId}.");
                }
            }

            // Coordinate transform validation guard
            if (observation.Transform.OutputWidthPx <= 0 || observation.Transform.OutputHeightPx <= 0 ||
                observation.Transform.OutputToSourceScaleX <= 0 || observation.Transform.OutputToSourceScaleY <= 0)
            {
                _logger.LogWarning("Invalid coordinate transform parameters in observation.");
                return new ScreenFusionResult(FusionStatus.InvalidCoordinateTransform, Array.Empty<FusionCandidate>(), null, "Coordinate transform parameters are invalid or non-positive.");
            }

            var candidates = new List<FusionCandidate>();
            var normalizedTarget = NormalizeText(targetQuery);

            foreach (var element in observation.UiaElements)
            {
                var elementText = element.Name ?? element.AutomationId ?? string.Empty;
                var normalizedElement = NormalizeText(elementText);
                var elementBounds = new ScreenRect(element.BoundingX, element.BoundingY, element.BoundingWidth, element.BoundingHeight);

                double textSimilarity = 0.0;
                if (!string.IsNullOrEmpty(normalizedTarget))
                {
                    if (normalizedElement.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        textSimilarity = 1.0;
                    }
                    else if (normalizedElement.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
                             normalizedTarget.Contains(normalizedElement, StringComparison.OrdinalIgnoreCase))
                    {
                        textSimilarity = 0.75;
                    }
                }

                OcrLine? matchedLine = null;
                double bestIoU = 0.0;

                foreach (var line in observation.OcrResult.Lines)
                {
                    var normalizedLine = NormalizeText(line.Text);
                    bool lineMatchesText = !string.IsNullOrEmpty(normalizedTarget) && (normalizedLine.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase) || normalizedTarget.Contains(normalizedLine, StringComparison.OrdinalIgnoreCase));

                    // Map line bounding polygon through canonical FrameCoordinateTransform
                    var mappedPolygon = observation.Transform.MapOutputPolygonToAbsoluteDesktop(line.Bounds);
                    var absTopLeft = mappedPolygon.AbsoluteTopLeft;
                    var absBottomRight = mappedPolygon.AbsoluteBottomRight;
                    var ocrRect = new ScreenRect((int)absTopLeft.X, (int)absTopLeft.Y, Math.Max(1, (int)(absBottomRight.X - absTopLeft.X)), Math.Max(1, (int)(absBottomRight.Y - absTopLeft.Y)));

                    double iou = ComputeIoU(elementBounds, ocrRect);

                    if (lineMatchesText || iou > 0.0)
                    {
                        if (iou >= bestIoU)
                        {
                            bestIoU = iou;
                            matchedLine = line;
                        }
                        if (lineMatchesText)
                        {
                            textSimilarity = Math.Max(textSimilarity, 0.90);
                        }
                    }
                }

                double geometryScore = bestIoU > 0.0 ? Math.Clamp(bestIoU * 1.5, 0.1, 1.0) : 0.0;
                double controlTypeScore = element.ControlType.Equals("Button", StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.8;
                double semanticPriority = 1.0; // UIA authoritative reference

                double totalScore = (geometryScore * 0.3) + (textSimilarity * 0.4) + (controlTypeScore * 0.2) + (semanticPriority * 0.1);

                // Strict rejection of false candidates: must have non-zero text similarity or geometric overlap matching target query
                if (textSimilarity >= 0.7 || (geometryScore > 0.0 && textSimilarity >= 0.4))
                {
                    var breakdown = new FusionScoreBreakdown(geometryScore, textSimilarity, controlTypeScore, semanticPriority, totalScore);
                    candidates.Add(new FusionCandidate(
                        ElementId: element.AutomationId ?? element.Name ?? "unknown",
                        ControlType: element.ControlType,
                        ElementName: elementText,
                        ElementBounds: elementBounds,
                        MatchedOcrLine: matchedLine,
                        MatchedText: matchedLine?.Text ?? elementText,
                        ScoreBreakdown: breakdown,
                        SourceHwnd: observation.Hwnd,
                        SourceProcessId: observation.ProcessId,
                        FrameId: observation.Frame.Metadata?.FrameId ?? string.Empty,
                        CapturedAt: observation.Timestamp,
                        OcrEngine: observation.OcrResult.Engine,
                        RecognizedLanguage: observation.OcrResult.RecognizedLanguage,
                        IsAmbiguous: false));
                }
            }

            if (candidates.Count == 0)
            {
                return new ScreenFusionResult(FusionStatus.NoMatch, Array.Empty<FusionCandidate>(), null, "No matching UIA or OCR elements found for query.");
            }

            // Sort by total score descending
            candidates.Sort((a, b) => b.ScoreBreakdown.TotalScore.CompareTo(a.ScoreBreakdown.TotalScore));

            // Ambiguity check: if top 2 candidates have very close scores
            bool isAmbiguous = candidates.Count > 1 && Math.Abs(candidates[0].ScoreBreakdown.TotalScore - candidates[1].ScoreBreakdown.TotalScore) < 0.05;

            if (isAmbiguous)
            {
                var marked = candidates.Select(c => c with { IsAmbiguous = true }).ToList();
                return new ScreenFusionResult(FusionStatus.Ambiguous, marked, null, "Multiple matching elements found with ambiguous scores.");
            }

            return new ScreenFusionResult(FusionStatus.Matched, candidates, candidates[0], null);
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.Normalize(System.Text.NormalizationForm.FormKC)
                       .ToLowerInvariant()
                       .Trim();
        }

        private static double ComputeIoU(ScreenRect r1, ScreenRect r2)
        {
            int x1 = Math.Max(r1.X, r2.X);
            int y1 = Math.Max(r1.Y, r2.Y);
            int x2 = Math.Min(r1.X + r1.Width, r2.X + r2.Width);
            int y2 = Math.Min(r1.Y + r1.Height, r2.Y + r2.Height);

            if (x2 <= x1 || y2 <= y1) return 0.0;

            int intersection = (x2 - x1) * (y2 - y1);
            int area1 = r1.Width * r1.Height;
            int area2 = r2.Width * r2.Height;
            int union = area1 + area2 - intersection;

            return union > 0 ? (double)intersection / union : 0.0;
        }
    }
}
