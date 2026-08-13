using System;
using System.Collections.Generic;
using MahmoudAI.Core.Automation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public sealed class ScreenFusionEngineTests
    {
        [Fact]
        public void ScreenFusionEngine_MatchesTargetWhenUiaAndOcrAgree()
        {
            var engine = new ScreenFusionEngine(NullLogger<ScreenFusionEngine>.Instance);
            var now = DateTimeOffset.UtcNow;

            var metadata = new ScreenFrameMetadata("f1", now, 100, 100, 400, 1.0f, 1.0f, 0, 0, 123, (nint)456);
            var transform = new FrameCoordinateTransform(
                new ScreenRect(0, 0, 800, 600), new ScreenRect(0, 0, 100, 100), 100, 100, 1.0, 1.0, CoordinateSpace.AbsoluteDesktopPhysicalPixels);

            var ocrLine = new OcrLine("Save", Array.Empty<OcrWord>(), new ScreenPolygon(
                new ScreenPoint(10, 10), new ScreenPoint(50, 10), new ScreenPoint(50, 30), new ScreenPoint(10, 30),
                new ScreenPoint(10, 10), new ScreenPoint(50, 30)));
            var ocrResult = new OcrResult(OcrStatus.Success, "TestEngine", "en", new[] { ocrLine }, "Save");

            var uiaElements = new[]
            {
                new UiaElementSnapshot("btnSave", "Save", "Button", "ButtonClass", "Win32", 123, 10, 10, 40, 20, true, false, Array.Empty<string>())
            };

            var observation = new ScreenObservation((nint)456, 123, now, metadata, transform, ocrResult, uiaElements, TimeSpan.FromSeconds(5));

            var result = engine.Fuse(observation, "Save");

            Assert.Equal(FusionStatus.Matched, result.Status);
            Assert.NotNull(result.BestCandidate);
            Assert.Equal("btnSave", result.BestCandidate.ElementId);
        }

        [Fact]
        public void ScreenFusionEngine_DetectsAmbiguityForDuplicateButtons()
        {
            var engine = new ScreenFusionEngine(NullLogger<ScreenFusionEngine>.Instance);
            var now = DateTimeOffset.UtcNow;

            var metadata = new ScreenFrameMetadata("f2", now, 100, 100, 400, 1.0f, 1.0f, 0, 0, 123, (nint)456);
            var transform = new FrameCoordinateTransform(
                new ScreenRect(0, 0, 800, 600), new ScreenRect(0, 0, 100, 100), 100, 100, 1.0, 1.0, CoordinateSpace.AbsoluteDesktopPhysicalPixels);

            var ocrResult = new OcrResult(OcrStatus.Success, "TestEngine", "en", Array.Empty<OcrLine>(), string.Empty);

            var uiaElements = new[]
            {
                new UiaElementSnapshot("btn1", "Save", "Button", "ButtonClass", "Win32", 123, 10, 10, 40, 20, true, false, Array.Empty<string>()),
                new UiaElementSnapshot("btn2", "Save", "Button", "ButtonClass", "Win32", 123, 60, 10, 40, 20, true, false, Array.Empty<string>())
            };

            var observation = new ScreenObservation((nint)456, 123, now, metadata, transform, ocrResult, uiaElements, TimeSpan.FromSeconds(5));

            var result = engine.Fuse(observation, "Save");

            Assert.Equal(FusionStatus.Ambiguous, result.Status);
            Assert.Null(result.BestCandidate);
            Assert.True(result.Candidates[0].IsAmbiguous);
        }

        [Fact]
        public void ScreenFusionEngine_RejectsStaleObservations()
        {
            var engine = new ScreenFusionEngine(NullLogger<ScreenFusionEngine>.Instance);
            var past = DateTimeOffset.UtcNow.AddSeconds(-10);

            var metadata = new ScreenFrameMetadata("f3", past, 100, 100, 400, 1.0f, 1.0f, 0, 0, 123, (nint)456);
            var transform = new FrameCoordinateTransform(
                new ScreenRect(0, 0, 800, 600), new ScreenRect(0, 0, 100, 100), 100, 100, 1.0, 1.0, CoordinateSpace.AbsoluteDesktopPhysicalPixels);
            var ocrResult = new OcrResult(OcrStatus.Success, "TestEngine", "en", Array.Empty<OcrLine>(), string.Empty);

            var observation = new ScreenObservation((nint)456, 123, past, metadata, transform, ocrResult, Array.Empty<UiaElementSnapshot>(), TimeSpan.FromSeconds(2));

            var result = engine.Fuse(observation, "Save");

            Assert.Equal(FusionStatus.StaleObservation, result.Status);
        }

        [Fact]
        public void ScreenFusionEngine_RejectsProcessMismatch()
        {
            var engine = new ScreenFusionEngine(NullLogger<ScreenFusionEngine>.Instance);
            var now = DateTimeOffset.UtcNow;

            var metadata = new ScreenFrameMetadata("f4", now, 100, 100, 400, 1.0f, 1.0f, 0, 0, 123, (nint)456);
            var transform = new FrameCoordinateTransform(
                new ScreenRect(0, 0, 800, 600), new ScreenRect(0, 0, 100, 100), 100, 100, 1.0, 1.0, CoordinateSpace.AbsoluteDesktopPhysicalPixels);
            var ocrResult = new OcrResult(OcrStatus.Success, "TestEngine", "en", Array.Empty<OcrLine>(), string.Empty);

            var uiaElements = new[]
            {
                new UiaElementSnapshot("btn1", "Save", "Button", "ButtonClass", "Win32", 999, 10, 10, 40, 20, true, false, Array.Empty<string>())
            };

            var observation = new ScreenObservation((nint)456, 123, now, metadata, transform, ocrResult, uiaElements, TimeSpan.FromSeconds(5));

            var result = engine.Fuse(observation, "Save");

            Assert.Equal(FusionStatus.ProcessMismatch, result.Status);
        }

        [Fact]
        public void ScreenFusionEngine_HandlesCroppedAndDownscaledFramesCorrectly()
        {
            var engine = new ScreenFusionEngine(NullLogger<ScreenFusionEngine>.Instance);
            var now = DateTimeOffset.UtcNow;

            var metadata = new ScreenFrameMetadata("f5", now, 200, 200, 400, 1.0f, 1.0f, 0, 0, 123, (nint)456);

            // OCR line in local output coordinates (scaled 2x back to region)
            var ocrLine = new OcrLine("Submit", Array.Empty<OcrWord>(), new ScreenPolygon(
                new ScreenPoint(5, 5), new ScreenPoint(50, 5), new ScreenPoint(50, 20), new ScreenPoint(5, 20),
                new ScreenPoint(5, 5), new ScreenPoint(50, 20)));
            var ocrResult = new OcrResult(OcrStatus.Success, "TestEngine", "en", new[] { ocrLine }, "Submit");

            var uiaElements = new[]
            {
                new UiaElementSnapshot("btnSubmit", "Submit", "Button", "ButtonClass", "Win32", 123, 10, 10, 90, 30, true, false, Array.Empty<string>())
            };

            // Transform with scale 2.0 (output width 200 mapped to region width 400)
            var transform = new FrameCoordinateTransform(
                SourceWindowBoundsPx: new ScreenRect(100, 100, 800, 600),
                SourceRegionPx: new ScreenRect(10, 10, 400, 400),
                OutputWidthPx: 200,
                OutputHeightPx: 200,
                OutputToSourceScaleX: 2.0,
                OutputToSourceScaleY: 2.0,
                CoordinateSpace: CoordinateSpace.AbsoluteDesktopPhysicalPixels);

            var observation = new ScreenObservation((nint)456, 123, now, metadata, transform, ocrResult, uiaElements, TimeSpan.FromSeconds(5));

            var result = engine.Fuse(observation, "Submit");

            Assert.Equal(FusionStatus.Matched, result.Status);
            Assert.NotNull(result.BestCandidate);
            Assert.Equal("btnSubmit", result.BestCandidate.ElementId);
        }
    }
}
