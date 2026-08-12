using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Media
{
    public interface IVoiceAdapter
    {
        Task<string> SpeechToTextAsync(byte[] audioData, CancellationToken ct);
        Task<byte[]> TextToSpeechAsync(string text, string language, CancellationToken ct);
    }

    public interface IVisionOcrAdapter
    {
        Task<string> ExtractTextFromScreenAsync(CancellationToken ct);
    }

    public class DefaultVoiceAdapter : IVoiceAdapter
    {
        private readonly ILogger<DefaultVoiceAdapter> _logger;

        public DefaultVoiceAdapter(ILogger<DefaultVoiceAdapter> logger)
        {
            _logger = logger;
        }

        public Task<string> SpeechToTextAsync(byte[] audioData, CancellationToken ct)
        {
            _logger.LogWarning("Speech-to-text requested without Whisper.net model or audio stream integration.");
            throw new InvalidOperationException("Whisper.net multilingual speech recognition engine not configured or initialized.");
        }

        public Task<byte[]> TextToSpeechAsync(string text, string language, CancellationToken ct)
        {
            _logger.LogWarning("Text-to-speech requested without active TTS engine.");
            throw new InvalidOperationException("Text-to-speech audio synthesis engine not configured.");
        }
    }
}
