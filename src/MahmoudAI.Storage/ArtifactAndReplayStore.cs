using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Storage
{
    public class ArtifactAndReplayStore
    {
        private readonly string _connectionString;
        private readonly ILogger<ArtifactAndReplayStore> _logger;

        public ArtifactAndReplayStore(string dbPath, ILogger<ArtifactAndReplayStore> logger)
        {
            _connectionString = $"Data Source={dbPath}";
            _logger = logger;
            InitializeTables();
        }

        private void InitializeTables()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS artifacts (
                    artifact_id TEXT PRIMARY KEY,
                    mission_id TEXT NOT NULL,
                    name TEXT NOT NULL,
                    file_path TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS replay_sessions (
                    session_id TEXT PRIMARY KEY,
                    mission_id TEXT NOT NULL,
                    steps_json TEXT NOT NULL,
                    recorded_at TEXT NOT NULL
                );
            ";
            cmd.ExecuteNonQuery();
            _logger.LogInformation("Artifact and replay tables initialized.");
        }

        public async Task SaveArtifactAsync(string missionId, string name, string filePath, CancellationToken ct)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO artifacts (artifact_id, mission_id, name, file_path, created_at)
                VALUES (@id, @missionId, @name, @path, @time);
            ";
            cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("N"));
            cmd.Parameters.AddWithValue("@missionId", missionId);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@path", filePath);
            cmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
