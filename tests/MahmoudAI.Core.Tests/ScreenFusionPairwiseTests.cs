using System;
using System.Collections.Generic;
using MahmoudAI.Core.Automation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public sealed class ScreenFusionPairwiseTests
    {
        [Fact]
        public void ScreenFusionEngine_PreventsCrossLineEvidenceMixup()
        {
            var engine = new ScreenFusionEngine(NullLogger<ScreenFusionEngine>.Instance);
            var now = DateTimeOffset.UtcNow;

            var metadata = new ScreenFrameMetadata("f_pair", now, 200, 200, 800, 1.0f, 1.0f, 0, 0, 123, (nint)456);
            using var frame = new RedactedScreenFrame(ScreenCaptureStatus.Captured, metadata, new byte[800], 0);

            // Line 1 contains text "Save" but is far away from the button
            var lineFarSave = new OcrLine("Save", Array.Empty<OcrWord>(), new ScreenPolygon(
                new ScreenPoint(10, 150), new ScreenPoint(50, 150), new ScreenPoint(50, 170), new ScreenPoint(10, 170),
                new ScreenPoint(10, 150), new ScreenPoint(50, 170)));

            // Line 2 contains unrelated text "Cancel" but overlaps the UIA button bounds
            var lineCloseCancel = new OcrLine("Cancel", Array.Empty<OcrWord>(), new ScreenPolygon(
                new ScreenPoint(10, 10), new ScreenPoint(50, 10), new ScreenPoint(50, 30), new ScreenPoint(10, 30),
                new ScreenPoint(10, 10), new ScreenPoint(50, 30)));

            var ocrResult = new OcrResult(OcrStatus.Success, "TestEngine", "en", new[] { lineFarSave, lineCloseCancel }, "Save Cancel");

            // UIA button named "Save" at top-left
            var uiaElements = new[]
            {
                new UiaElementSnapshot("btnSave", "Save", "Button", "ButtonClass", "Win32", 123, 10, 10, 40, 20, true, false, Array.Empty<string>())
            };

            var transform = new FrameCoordinateTransform(
                SourceWindowBoundsPx: new ScreenRect(0, 0, 800, 600),
                SourceRegionPx: new ScreenRect(0, 0, 200, 200),
                OutputWidthPx: 200,
                OutputHeightPx: 200,
                OutputToSourceScaleX: 1.0,
                OutputToSourceScaleY: 1.0,
                CoordinateSpace: CoordinateSpace.AbsoluteDesktopPhysicalPixels);

            var observation = new ScreenObservation((nint)456, 123, now, frame, ocrResult, uiaElements, transform, TimeSpan.FromSeconds(5));

            var result = engine.Fuse(observation, "Save");

            Assert.Equal(FusionStatus.Matched, result.Status);
            Assert.NotNull(result.BestCandidate);
            // Must match btnSave via UIA name "Save" and close line "Cancel", without picking the distant "Save" line for geometry
            Assert.Equal("btnSave", result.BestCandidate.ElementId);
            Assert.NotNull(result.BestCandidate.MatchedOcrLine);
            // The matched line should be the one overlapping spatially (lineCloseCancel) rather than the distant text line
            Assert.Equal("Cancel", result.BestCandidate.MatchedOcrLine.Text);
        }
    }
}
