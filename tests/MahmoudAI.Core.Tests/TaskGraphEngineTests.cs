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
    }
}
