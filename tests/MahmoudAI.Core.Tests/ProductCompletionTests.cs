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
        public async Task AiProviderClient_ShouldThrowOnUnreachableEndpoint()
        {
            var client = new AiProviderClient(NullLogger<AiProviderClient>.Instance);
            Func<Task> act = async () => await client.GenerateCompletionAsync("llama3", "Hello", "http://localhost:11434", null, CancellationToken.None);
            await act.Should().ThrowAsync<HttpRequestException>();
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
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath))
                {
                    try { File.Delete(dbPath); } catch { }
                }
            }
        }

        [Fact]
        public async Task ScreenOcrService_ShouldThrowWhenPlatformNotSupported()
        {
            var ocr = new ScreenOcrService(NullLogger<ScreenOcrService>.Instance);
            Func<Task> act = async () => await ocr.CaptureAndExtractTextAsync(CancellationToken.None);
            await act.Should().ThrowAsync<PlatformNotSupportedException>();
        }
    }
}
