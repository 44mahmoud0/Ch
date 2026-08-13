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
        public void ScreenFusionEngine_SuccessfullyMatchesUniqueElement()
        {
            var engine = new ScreenFusionEngine(NullLogger<ScreenFusionEngine>.Instance);
            var now = DateTimeOffset.UtcNow;

            var metadata = new ScreenFrameMetadata("f1", now, 100, 100, 400, 1.0f, 1.0f, 0, 0, 123, (nint)456);
            using var frame = new RedactedScreenFrame(ScreenCaptureStatus.Captured, metadata, new byte[400], 0);

            var ocrLine = new OcrLine("Save", Array.Empty<OcrWord>(), new ScreenPolygon(
                new ScreenPoint(10, 10), new ScreenPoint(50, 10), new ScreenPoint(50, 30), new ScreenPoint(10, 30),
                new ScreenPoint(10, 10), new ScreenPoint(50, 30)));
            var ocrResult = new OcrResult(OcrStatus.Success, "TestEngine", "en", new[] { ocrLine }, "Save");

            var uiaElements = new[]
            {
                new UiaElementSnapshot("btn1", "Save", "Button", "ButtonClass", "Win32", 123, 10, 10, 40, 20, true, false, Array.Empty<string>())
            };

            var transform = new FrameCoordinateTransform(
                new ScreenRect(0, 0, 800, 600), new ScreenRect(0, 0, 100, 100), 100, 100, 1.0, 1.0, CoordinateSpace.AbsoluteDesktopPhysicalPixels);

            var observation = new ScreenObservation((nint)456, 123, now, frame, ocrResult, uiaElements, transform, TimeSpan.FromSeconds(5));

            var result = engine.Fuse(observation, "Save");

            Assert.Equal(FusionStatus.Matched, result.Status);
            Assert.NotNull(result.BestCandidate);
            Assert.Equal("btn1", result.BestCandidate.ElementId);
            Assert.False(result.BestCandidate.IsAmbiguous);
        }

        [Fact]
        public void ScreenFusionEngine_DetectsAmbiguityForDuplicateButtons()
        {
            var engine = new ScreenFusionEngine(NullLogger<ScreenFusionEngine>.Instance);
            var now = DateTimeOffset.UtcNow;

            var metadata = new ScreenFrameMetadata("f2", now, 100, 100, 400, 1.0f, 1.0f, 0, 0, 123, (nint)456);
            using var frame = new RedactedScreenFrame(ScreenCaptureStatus.Captured, metadata, new byte[400], 0);

            var ocrResult = new OcrResult(OcrStatus.Success, "TestEngine", "en", Array.Empty<OcrLine>(), string.Empty);

            var uiaElements = new[]
            {
                new UiaElementSnapshot("btn1", "Save", "Button", "ButtonClass", "Win32", 123, 10, 10, 40, 20, true, false, Array.Empty<string>()),
                new UiaElementSnapshot("btn2", "Save", "Button", "ButtonClass", "Win32", 123, 60, 10, 40, 20, true, false, Array.Empty<string>())
            };

            var transform = new FrameCoordinateTransform(
                new ScreenRect(0, 0, 800, 600), new ScreenRect(0, 0, 100, 100), 100, 100, 1.0, 1.0, CoordinateSpace.AbsoluteDesktopPhysicalPixels);

            var observation = new ScreenObservation((nint)456, 123, now, frame, ocrResult, uiaElements, transform, TimeSpan.FromSeconds(5));

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
            using var frame = new RedactedScreenFrame(ScreenCaptureStatus.Captured, metadata, new byte[400], 0);
            var ocrResult = new OcrResult(OcrStatus.Success, "TestEngine", "en", Array.Empty<OcrLine>(), string.Empty);
            var transform = new FrameCoordinateTransform(
                new ScreenRect(0, 0, 800, 600), new ScreenRect(0, 0, 100, 100), 100, 100, 1.0, 1.0, CoordinateSpace.AbsoluteDesktopPhysicalPixels);

            var observation = new ScreenObservation((nint)456, 123, past, frame, ocrResult, Array.Empty<UiaElementSnapshot>(), transform, TimeSpan.FromSeconds(2));

            var result = engine.Fuse(observation, "Save");

            Assert.Equal(FusionStatus.StaleObservation, result.Status);
        }
    }
}
