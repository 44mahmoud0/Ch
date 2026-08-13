using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Engine.TaskGraph;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Storage
{
    public sealed class SqliteMissionStore
    {
        private readonly string _connectionString;
        private readonly ILogger<SqliteMissionStore> _logger;

        public SqliteMissionStore(string dbPath, ILogger<SqliteMissionStore> logger)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
            ArgumentNullException.ThrowIfNull(logger);

            if (!string.Equals(dbPath, ":memory:", StringComparison.OrdinalIgnoreCase))
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(dbPath));
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = string.Equals(dbPath, ":memory:", StringComparison.OrdinalIgnoreCase)
                    ? SqliteOpenMode.Memory
                    : SqliteOpenMode.ReadWriteCreate,
                Cache = string.Equals(dbPath, ":memory:", StringComparison.OrdinalIgnoreCase)
                    ? SqliteCacheMode.Shared
                    : SqliteCacheMode.Default
            }.ToString();
            _logger = logger;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA busy_timeout = 5000;
                CREATE TABLE IF NOT EXISTS missions (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    objective TEXT NOT NULL,
                    status TEXT NOT NULL,
                    priority TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    completed_at TEXT,
                    error_message TEXT
                );
                CREATE TABLE IF NOT EXISTS memory_vectors (
                    id TEXT PRIMARY KEY,
                    content TEXT NOT NULL,
                    metadata TEXT,
                    created_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS mission_checkpoints (
                    mission_id TEXT PRIMARY KEY,
                    state_json TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS mission_events (
                    id TEXT PRIMARY KEY,
                    mission_id TEXT NOT NULL,
                    event_type TEXT NOT NULL,
                    payload TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_mission_events_mission_created
                    ON mission_events (mission_id, created_at);
            ";
            command.ExecuteNonQuery();
            _logger.LogInformation("SQLite mission store initialized successfully.");
        }

        public async Task SaveMissionAsync(
            string id,
            string title,
            string objective,
            string status,
            string priority,
            CancellationToken cancellationToken)
        {
            using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO missions (id, title, objective, status, priority, created_at)
                VALUES (@id, @title, @objective, @status, @priority, @createdAt);
            ";
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@objective", objective);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@priority", priority);
            command.Parameters.AddWithValue("@createdAt", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task SaveMemoryAsync(
            string id,
            string content,
            string metadata,
            CancellationToken cancellationToken)
        {
            using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO memory_vectors (id, content, metadata, created_at)
                VALUES (@id, @content, @metadata, @createdAt);
            ";
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@content", content);
            command.Parameters.AddWithValue("@metadata", metadata ?? string.Empty);
            command.Parameters.AddWithValue("@createdAt", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task SaveCheckpointAsync(string missionId, string stateJson, CancellationToken cancellationToken)
        {
            using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO mission_checkpoints (mission_id, state_json, updated_at)
                VALUES (@missionId, @stateJson, @updatedAt);
            ";
            command.Parameters.AddWithValue("@missionId", missionId);
            command.Parameters.AddWithValue("@stateJson", stateJson);
            command.Parameters.AddWithValue("@updatedAt", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<string?> GetCheckpointAsync(string missionId, CancellationToken cancellationToken)
        {
            using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT state_json FROM mission_checkpoints WHERE mission_id = @missionId;";
            command.Parameters.AddWithValue("@missionId", missionId);
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result?.ToString();
        }

        public async Task SaveMissionEventAsync(MissionTaskEvent evt, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(evt);

            var eventId = ComputeEventId(evt);
            var payload = JsonSerializer.Serialize(evt);
            using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO mission_events
                    (id, mission_id, event_type, payload, created_at)
                VALUES
                    (@id, @missionId, @eventType, @payload, @createdAt);
            ";
            command.Parameters.AddWithValue("@id", eventId);
            command.Parameters.AddWithValue("@missionId", evt.MissionId);
            command.Parameters.AddWithValue("@eventType", evt.Type.ToString());
            command.Parameters.AddWithValue("@payload", payload);
            command.Parameters.AddWithValue("@createdAt", evt.Timestamp.UtcDateTime.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> GetMissionEventCountAsync(string missionId, CancellationToken cancellationToken)
        {
            using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM mission_events WHERE mission_id = @missionId;";
            command.Parameters.AddWithValue("@missionId", missionId);
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result);
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }

        private static string ComputeEventId(MissionTaskEvent evt)
        {
            var canonical = string.Join("|", evt.MissionId, evt.TaskId, evt.Type, evt.Timestamp.UtcTicks, evt.Attempt, evt.Message ?? string.Empty);
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(digest);
        }
    }

    public sealed class SqliteMissionEventSink : IMissionEventSink
    {
        private readonly SqliteMissionStore _store;

        public SqliteMissionEventSink(SqliteMissionStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public ValueTask EmitAsync(MissionTaskEvent evt, CancellationToken cancellationToken = default)
        {
            return new ValueTask(_store.SaveMissionEventAsync(evt, cancellationToken));
        }
    }
}
