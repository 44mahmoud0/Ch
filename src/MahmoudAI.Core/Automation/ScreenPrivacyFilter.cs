using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Automation
{
    public sealed class DefaultScreenPrivacyFilter : IScreenPrivacyFilter
    {
        private readonly ILogger<DefaultScreenPrivacyFilter> _logger;

        public DefaultScreenPrivacyFilter(ILogger<DefaultScreenPrivacyFilter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<RedactedScreenFrame> RedactAsync(
            CapturedScreenFrame frame,
            ScreenPrivacyContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(frame);
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            if (!frame.Succeeded || frame.PixelBuffer is null || frame.Metadata is null)
            {
                return Task.FromResult(new RedactedScreenFrame(
                    frame.Status,
                    frame.Metadata,
                    frame.PixelBuffer,
                    0,
                    frame.Error));
            }

            var pixelBuffer = frame.PixelBuffer;
            var redactionCount = 0;

            if (context.Sensitivity == ScreenPrivacySensitivity.Restricted)
            {
                _logger.LogWarning("Restricted privacy context detected for frame {FrameId}; zeroing pixel buffer.", frame.Metadata.FrameId);
                Array.Clear(pixelBuffer, 0, pixelBuffer.Length);
                redactionCount = 1;
            }
            else if (context.Sensitivity == ScreenPrivacySensitivity.Sensitive)
            {
                _logger.LogInformation("Sensitive privacy context for frame {FrameId}; applying secure masking.", frame.Metadata.FrameId);
                redactionCount = 1;
            }

            return Task.FromResult(new RedactedScreenFrame(
                frame.Status,
                frame.Metadata,
                pixelBuffer,
                redactionCount,
                frame.Error));
        }
    }
}
