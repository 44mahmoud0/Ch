using System;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Automation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public sealed class ScreenPrivacyFilterTests
    {
        [Fact]
        public async Task PublicContext_PassesFrameThroughUnmodified()
        {
            var filter = new DefaultScreenPrivacyFilter(NullLogger<DefaultScreenPrivacyFilter>.Instance);
            var pixels = new byte[] { 10, 20, 30, 40 };
            var metadata = new ScreenFrameMetadata(
                "frame-1",
                DateTimeOffset.UtcNow,
                1,
                1,
                4,
                1.0f,
                1.0f,
                0,
                0,
                100,
                (nint)123);
            using var frame = new CapturedScreenFrame(ScreenCaptureStatus.Captured, metadata, pixels);
            var context = new ScreenPrivacyContext(ScreenPrivacySensitivity.Public, true, true, true);

            using var redacted = await filter.RedactAsync(frame, context, CancellationToken.None);

            Assert.True(redacted.Succeeded);
            Assert.Equal(0, redacted.RedactionCount);
            Assert.NotNull(redacted.PixelBuffer);
            Assert.Equal(10, redacted.PixelBuffer[0]);
        }

        [Fact]
        public async Task RestrictedContext_ClearsPixelBuffer()
        {
            var filter = new DefaultScreenPrivacyFilter(NullLogger<DefaultScreenPrivacyFilter>.Instance);
            var pixels = new byte[] { 10, 20, 30, 40 };
            var metadata = new ScreenFrameMetadata(
                "frame-2",
                DateTimeOffset.UtcNow,
                1,
                1,
                4,
                1.0f,
                1.0f,
                0,
                0,
                100,
                (nint)123);
            using var frame = new CapturedScreenFrame(ScreenCaptureStatus.Captured, metadata, pixels);
            var context = new ScreenPrivacyContext(ScreenPrivacySensitivity.Restricted, false, false, false);

            using var redacted = await filter.RedactAsync(frame, context, CancellationToken.None);

            Assert.True(redacted.Succeeded);
            Assert.Equal(1, redacted.RedactionCount);
            Assert.NotNull(redacted.PixelBuffer);
            Assert.Equal(0, redacted.PixelBuffer[0]);
        }
    }
}
