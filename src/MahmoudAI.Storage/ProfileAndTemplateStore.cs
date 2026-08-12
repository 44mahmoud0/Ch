using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Storage
{
    public class ProfileAndTemplateStore
    {
        private readonly string _connectionString;
        private readonly ILogger<ProfileAndTemplateStore> _logger;

        public ProfileAndTemplateStore(string dbPath, ILogger<ProfileAndTemplateStore> logger)
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
                CREATE TABLE IF NOT EXISTS user_profiles (
                    username TEXT PRIMARY KEY,
                    preferences_json TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS mission_templates (
                    template_id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    definition_json TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS mission_timeline (
                    event_id TEXT PRIMARY KEY,
                    mission_id TEXT NOT NULL,
                    event_type TEXT NOT NULL,
                    details TEXT NOT NULL,
                    timestamp TEXT NOT NULL
                );
            ";
            cmd.ExecuteNonQuery();
            _logger.LogInformation("Profile, template, and timeline tables initialized.");
        }

        public async Task SaveProfileAsync(string username, string preferencesJson, CancellationToken ct)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO user_profiles (username, preferences_json, updated_at)
                VALUES (@username, @prefs, @time);
            ";
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@prefs", preferencesJson);
            cmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task LogTimelineEventAsync(string missionId, string eventType, string details, CancellationToken ct)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO mission_timeline (event_id, mission_id, event_type, details, timestamp)
                VALUES (@eventId, @missionId, @type, @details, @time);
            ";
            cmd.Parameters.AddWithValue("@eventId", Guid.NewGuid().ToString("N"));
            cmd.Parameters.AddWithValue("@missionId", missionId);
            cmd.Parameters.AddWithValue("@type", eventType);
            cmd.Parameters.AddWithValue("@details", details);
            cmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
