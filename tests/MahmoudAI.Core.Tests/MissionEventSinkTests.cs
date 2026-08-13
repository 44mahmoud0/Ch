using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MahmoudAI.Core.Engine.TaskGraph;
using MahmoudAI.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public sealed class MissionEventSinkTests
    {
        [Fact]
        public async Task SqliteSink_ShouldPersistAnEventIdempotently()
        {
            var dbPath = CreateTemporaryDatabasePath();
            try
            {
                var store = new SqliteMissionStore(dbPath, NullLogger<SqliteMissionStore>.Instance);
                var sink = new SqliteMissionEventSink(store);
                var evt = new MissionTaskEvent(
                    "mission-1",
                    "task-1",
                    MissionTaskEventType.Succeeded,
                    DateTimeOffset.Parse("2026-08-13T00:00:00.0000000+00:00"),
                    1,
                    "completed");

                await sink.EmitAsync(evt);
                await sink.EmitAsync(evt);

                var count = await store.GetMissionEventCountAsync("mission-1", CancellationToken.None);
                count.Should().Be(1);
            }
            finally
            {
                DeleteDatabaseFiles(dbPath);
            }
        }

        [Fact]
        public async Task CompositeSink_ShouldDeliverToEveryChildSinkInOrder()
        {
            var first = new RecordingSink();
            var second = new RecordingSink();
            var sink = new CompositeMissionEventSink(new IMissionEventSink[] { first, second });
            var evt = NewEvent("mission-2", "task-2", MissionTaskEventType.Started);

            await sink.EmitAsync(evt);

            first.Events.Should().ContainSingle().Which.Should().Be(evt);
            second.Events.Should().ContainSingle().Which.Should().Be(evt);
        }

        [Fact]
        public async Task BufferedSink_ShouldDrainEventsBeforeDispose()
        {
            var recording = new RecordingSink();
            var sink = new BufferedMissionEventSink(recording, capacity: 2);
            var first = NewEvent("mission-3", "task-1", MissionTaskEventType.Queued);
            var second = NewEvent("mission-3", "task-2", MissionTaskEventType.Queued);

            await sink.EmitAsync(first);
            await sink.EmitAsync(second);
            await sink.DisposeAsync();

            recording.Events.Should().HaveCount(2);
            recording.Events.Should().ContainInOrder(first, second);
        }

        private static MissionTaskEvent NewEvent(string missionId, string taskId, MissionTaskEventType type)
        {
            return new MissionTaskEvent(
                missionId,
                taskId,
                type,
                DateTimeOffset.UtcNow,
                0,
                null);
        }

        private static string CreateTemporaryDatabasePath()
        {
            return Path.Combine(Path.GetTempPath(), $"mahmoud-ai-events-{Guid.NewGuid():N}.db");
        }

        private static void DeleteDatabaseFiles(string dbPath)
        {
            foreach (var path in new[] { dbPath, $"{dbPath}-wal", $"{dbPath}-shm" })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private sealed class RecordingSink : IMissionEventSink
        {
            public ConcurrentQueue<MissionTaskEvent> Events { get; } = new();

            public ValueTask EmitAsync(MissionTaskEvent evt, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Events.Enqueue(evt);
                return ValueTask.CompletedTask;
            }
        }
    }
}
