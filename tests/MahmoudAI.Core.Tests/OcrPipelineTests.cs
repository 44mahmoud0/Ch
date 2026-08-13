using System;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Automation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public sealed class OcrPipelineTests
    {
        [Fact]
        public async Task OcrPipeline_RejectsNullOrFailedFrames()
        {
            var primary = new StubOcrEngine(new OcrResult(OcrStatus.Success, "Stub", "en", Array.Empty<OcrLine>(), "stub"));
            var pipeline = new OcrPipeline(primary, null, NullLogger<OcrPipeline>.Instance);

            var metadata = new ScreenFrameMetadata("f1", DateTimeOffset.UtcNow, 10, 10, 40, 1.0f, 1.0f, 0, 0, 100, (nint)123);
            using var failedFrame = new RedactedScreenFrame(ScreenCaptureStatus.Denied, metadata, null, 0, null, "Denied");

            var result = await pipeline.RecognizeAsync(failedFrame, new OcrRequest(), CancellationToken.None);

            Assert.Equal(OcrStatus.EmptyImage, result.Status);
            Assert.Contains("Denied", result.Error);
        }

        [Fact]
        public async Task OcrPipeline_FallsBackWhenPrimaryFailsWithLanguageUnavailable()
        {
            var primary = new StubOcrEngine(new OcrResult(OcrStatus.LanguageUnavailable, "Stub1", "ar", Array.Empty<OcrLine>(), string.Empty, "Not installed"));
            var fallback = new StubOcrEngine(new OcrResult(OcrStatus.Success, "Stub2", "en", Array.Empty<OcrLine>(), "English Fallback"));
            var pipeline = new OcrPipeline(primary, fallback, NullLogger<OcrPipeline>.Instance);

            var metadata = new ScreenFrameMetadata("f2", DateTimeOffset.UtcNow, 10, 10, 40, 1.0f, 1.0f, 100, 200, 100, (nint)123);
            using var frame = new RedactedScreenFrame(ScreenCaptureStatus.Captured, metadata, new byte[40], 0);

            var result = await pipeline.RecognizeAsync(frame, new OcrRequest(OcrLanguageMode.Arabic), CancellationToken.None);

            Assert.Equal(OcrStatus.Success, result.Status);
            Assert.Equal("Stub2", result.Engine);
            Assert.Equal("English Fallback", result.FullText);
        }

        private sealed class StubOcrEngine : IOcrEngine
        {
            private readonly OcrResult _result;

            public StubOcrEngine(OcrResult result)
            {
                _result = result;
            }

            public Task<OcrResult> RecognizeAsync(RedactedScreenFrame frame, OcrRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_result);
            }
        }
    }
}
