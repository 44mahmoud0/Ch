using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MahmoudAI.Core.Engine;
using MahmoudAI.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public class TaskGraphEngineTests
    {
        [Fact]
        public async Task ExecuteGraphAsync_ShouldRespectDependencies()
        {
            var logger = NullLogger<TaskGraphEngine>.Instance;
            var engine = new TaskGraphEngine(logger);

            var executedOrder = new List<string>();

            var tasks = new List<MissionTask>
            {
                new MissionTask
                {
                    Id = "t1",
                    Name = "Task 1",
                    Action = async ct => { await Task.Delay(10, ct); executedOrder.Add("t1"); return true; }
                },
                new MissionTask
                {
                    Id = "t2",
                    Name = "Task 2",
                    Dependencies = { "t1" },
                    Action = async ct => { await Task.Delay(10, ct); executedOrder.Add("t2"); return true; }
                }
            };

            bool success = await engine.ExecuteGraphAsync(tasks, CancellationToken.None);

            success.Should().BeTrue();
            executedOrder.Should().ContainInOrder("t1", "t2");
        }

        [Fact]
        public async Task McpClient_ShouldRegisterAndProvideTools()
        {
            var logger = NullLogger<McpClientConnector>.Instance;
            var client = new McpClientConnector(logger);

            await client.RegisterServerAsync("TestServer", "http://localhost:5000", CancellationToken.None);
            var tools = client.GetAvailableTools();

            tools.Should().NotBeEmpty();
            tools.Should().Contain(t => t.Name == "mcp_filesystem_read");
        }

        [Fact]
        public async Task ExecuteGraphAsync_ShouldDetectCycles()
        {
            var logger = NullLogger<TaskGraphEngine>.Instance;
            var engine = new TaskGraphEngine(logger);

            var tasks = new List<MissionTask>
            {
                new MissionTask { Id = "t1", Name = "Task 1", Dependencies = { "t2" } },
                new MissionTask { Id = "t2", Name = "Task 2", Dependencies = { "t1" } }
            };

            bool success = await engine.ExecuteGraphAsync(tasks, CancellationToken.None);
            success.Should().BeFalse();
        }

        [Fact]
        public async Task AiProviderClient_ShouldThrowOnUnsupportedEndpoint()
        {
            var logger = NullLogger<AiProviderClient>.Instance;
            var client = new AiProviderClient(logger);

            Func<Task> act = async () => await client.GenerateCompletionAsync("llama3", "hello", "http://unsupported-endpoint:9999", null, CancellationToken.None);
            await act.Should().ThrowAsync<HttpRequestException>();
        }

        [Fact]
        public async Task ScreenOcrService_ShouldThrowWhenPlatformNotSupported()
        {
            var ocr = new ScreenOcrService(NullLogger<ScreenOcrService>.Instance);
            Func<Task> act = async () => await ocr.CaptureAndExtractTextAsync(CancellationToken.None);
            await act.Should().ThrowAsync<PlatformNotSupportedException>();
        }

        [Fact]
        public async Task DefaultVoiceAdapter_ShouldThrowWhenUnconfigured()
        {
            var voice = new DefaultVoiceAdapter(NullLogger<DefaultVoiceAdapter>.Instance);
            Func<Task> act = async () => await voice.SpeechToTextAsync(new byte[10], CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
