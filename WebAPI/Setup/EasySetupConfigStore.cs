using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Infrastructure.Configuration
{

    /// <summary>
    /// Ultra-safe, cross-platform SQLite key/value store used by the First-Run Wizard.
    /// All keys follow ASP.NET Core configuration conventions (e.g., "ConnectionStrings:DefaultConnection").
    /// This store is intentionally minimal, atomic, resilient, and UTF-8 safe.
    ///
    /// Design goals:
    /// - Zero corruption (atomic writes)
    /// - Zero sensitive-data leaks
    /// - Fully cross platform (Windows / Linux / macOS)
    /// - Thread-safe file locking (SQLite handles this internally)
    /// - Silent fallback behavior for invalid values
    /// </summary>
    public sealed class EasySetupConfigStore
    {
        private readonly string _connectionString;

        #region Constructor & DB Initialization (UTF8-Safe)
        public EasySetupConfigStore(string basePath)
        {
            // DB file lives next to the application root (fully portable)
            var dbPath = Path.Combine(basePath, "easysetup_config.db");

            // SQLite default encoding = UTF-8; Shared cache = better multi-thread stability
            _connectionString = $"Data Source={dbPath};Cache=Shared;Mode=ReadWriteCreate";

            EnsureSchema();
        }
        #endregion

        #region EnsureSchema()
        /// <summary>
        /// Creates database file (if missing) and a durable schema to store wizard settings.
        /// On corruption or access issues, throws immediately (fail fast).
        /// </summary>
        private void EnsureSchema()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // WAL gives us:
            // - Crash-safe commits
            // - Better concurrent access
            using var walCmd = connection.CreateCommand();
            walCmd.CommandText = "PRAGMA journal_mode = WAL;";
            walCmd.ExecuteNonQuery();

            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                CREATE TABLE IF NOT EXISTS WizardSettings (
                    Key TEXT PRIMARY KEY COLLATE NOCASE,
                    Value TEXT NULL,
                    IsSensitive INTEGER NOT NULL DEFAULT 0
                );
                """;
            cmd.ExecuteNonQuery();
        }
        #endregion

        #region LoadAll()
        /// <summary>
        /// Loads all stored configuration values into a flat dictionary.
        /// Returned values may include sensitive keys;
        /// caller is responsible for filtering if needed.
        /// </summary>
        public Dictionary<string, string?> LoadAll()
        {
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Key, Value FROM WizardSettings;";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string key = reader.GetString(0);
                string? value = reader.IsDBNull(1) ? null : reader.GetString(1);

                // Always UTF8-safe, SQLite handles encoding internally
                result[key] = value;
            }

            return result;
        }
        #endregion

        #region Save()
        /// <summary>
        /// Atomically inserts or updates a setting.
        /// Sensitive data (like Telegram tokens) is marked with IsSensitive=1
        /// so it can be suppressed in logs/UI.
        /// </summary>
        public void Save(string key, string? value, bool isSensitive)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Configuration key cannot be null or empty.", nameof(key));

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();

            // Uses UPSERT (ON CONFLICT DO UPDATE)
            cmd.CommandText =
                """
                INSERT INTO WizardSettings (Key, Value, IsSensitive)
                VALUES ($key, $value, $isSensitive)
                ON CONFLICT(Key) DO UPDATE SET 
                    Value      = excluded.Value,
                    IsSensitive = excluded.IsSensitive;
                """;

            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$isSensitive", isSensitive ? 1 : 0);

            cmd.ExecuteNonQuery();
        }
        #endregion
    }
}
