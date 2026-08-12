using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MahmoudAI.Core.Media;
using MahmoudAI.Core.Runtime;
using MahmoudAI.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public class ProductCompletionTests
    {
        [Fact]
        public async Task AiProviderClient_ShouldHandleLocalOllamaOrFallback()
        {
            var client = new AiProviderClient(NullLogger<AiProviderClient>.Instance);
            string result = await client.GenerateCompletionAsync("llama3", "Hello", "http://localhost:11434", null, CancellationToken.None);
            result.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ArtifactAndReplayStore_ShouldPersistArtifacts()
        {
            string dbPath = Path.Combine(Path.GetTempPath(), $"test_artifact_{System.Guid.NewGuid():N}.db");
            try
            {
                var store = new ArtifactAndReplayStore(dbPath, NullLogger<ArtifactAndReplayStore>.Instance);
                await store.SaveArtifactAsync("m_1", "Report.md", "/path/to/report.md", CancellationToken.None);
                File.Exists(dbPath).Should().BeTrue();
            }
            finally
            {
                if (File.Exists(dbPath)) File.Delete(dbPath);
            }
        }

        [Fact]
        public async Task ScreenOcrService_ShouldExtractText()
        {
            var ocr = new ScreenOcrService(NullLogger<ScreenOcrService>.Instance);
            string text = await ocr.CaptureAndExtractTextAsync(CancellationToken.None);
            text.Should().Contain("Mahmoud AI Desktop");
        }
    }
}
