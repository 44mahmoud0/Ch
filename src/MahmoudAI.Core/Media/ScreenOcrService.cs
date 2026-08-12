using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Media
{
    public class ScreenOcrService
    {
        private readonly ILogger<ScreenOcrService> _logger;

        public ScreenOcrService(ILogger<ScreenOcrService> logger)
        {
            _logger = logger;
        }

        public Task<string> CaptureAndExtractTextAsync(CancellationToken ct)
        {
            _logger.LogWarning("Screen OCR requested outside a native Windows 11 desktop runtime context.");
            throw new PlatformNotSupportedException("Screen capture and OCR require native Windows.Graphics.Capture and Windows.Media.Ocr APIs available on Windows 11.");
        }
    }
}
