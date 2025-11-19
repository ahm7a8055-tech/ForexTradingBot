using Microsoft.Data.Sqlite;

namespace Infrastructure.Configuration
{
    public sealed class EasySetupConfigStore
    {
        private readonly string _connectionString;
        private readonly ILogger<EasySetupConfigStore>? _logger;

        // ============================================================
        #region Constructor & Initialization
        // ============================================================
        public EasySetupConfigStore(string basePath, ILogger<EasySetupConfigStore>? logger = null)
        {
            _logger = logger;

            try
            {
                var dbPath = Path.Combine(basePath, "easysetup_config.db");

                // FIX: Ensure the directory exists before SQLite tries to open the file
                var directory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _connectionString = $"Data Source={dbPath};Cache=Shared;Mode=ReadWriteCreate";

                _logger?.LogInformation("Initializing EasySetupConfigStore at path: {DbPath}", dbPath);

                EnsureSchema();
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Failed to initialize EasySetupConfigStore.");
                throw;
            }
        }
        #endregion



        // ============================================================
        #region Schema Initialization
        // ============================================================
        private void EnsureSchema()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var walCmd = connection.CreateCommand();
                walCmd.CommandText = "PRAGMA journal_mode = WAL;";
                walCmd.ExecuteNonQuery();
                _logger?.LogInformation("SQLite journal mode set to WAL.");

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

                _logger?.LogInformation("SQLite schema ensured successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Schema initialization failed.");
                throw;
            }
        }
        #endregion



        // ============================================================
        #region SAFE LoadAll
        // ============================================================
        public OperationResult<Dictionary<string, string?>> LoadAllSafe()
        {
            try
            {
                var data = LoadAll();
                return OperationResult<Dictionary<string, string?>>.Ok(data);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "LoadAll failed.");

                return OperationResult<Dictionary<string, string?>>.Fail(
                    $"LoadAll failed: {ex.Message}"
                );
            }
        }
        #endregion



        // ============================================================
        #region LoadAll (Raw)
        // ============================================================
        public Dictionary<string, string?> LoadAll()
        {
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT Key, Value FROM WizardSettings;";
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string key = reader.GetString(0);
                    string? value = reader.IsDBNull(1) ? null : reader.GetString(1);

                    result[key] = value;
                }

                _logger?.LogInformation("LoadAll retrieved {Count} configuration entries.", result.Count);
            }
            catch
            {
                _logger?.LogError("Unexpected error occurred during LoadAll execution.");
                throw;
            }

            return result;
        }
        #endregion



        // ============================================================
        #region SAFE Save
        // ============================================================
        public OperationResult<bool> SaveSafe(string key, string? value, bool isSensitive)
        {
            try
            {
                Save(key, value, isSensitive);
                return OperationResult<bool>.Ok(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Save failed for key: {Key}", key);

                return OperationResult<bool>.Fail(
                    $"Save failed for key '{key}': {ex.Message}"
                );
            }
        }
        #endregion



        // ============================================================
        #region Save (Raw)
        // ============================================================
        public void Save(string key, string? value, bool isSensitive)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger?.LogWarning("Attempted to save a null/empty configuration key.");
                throw new ArgumentException("Configuration key cannot be empty.", nameof(key));
            }

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText =
                """
                INSERT INTO WizardSettings (Key, Value, IsSensitive)
                VALUES ($key, $value, $isSensitive)
                ON CONFLICT(Key) DO UPDATE SET
                    Value       = excluded.Value,
                    IsSensitive = excluded.IsSensitive;
                """;

                cmd.Parameters.AddWithValue("$key", key);
                cmd.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$isSensitive", isSensitive ? 1 : 0);

                cmd.ExecuteNonQuery();

                _logger?.LogInformation(
                    "Key '{Key}' was saved successfully. Sensitive: {IsSensitive}",
                    key,
                    isSensitive
                );
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error while saving key: {Key}", key);
                throw;
            }
        }
        #endregion
    }



    // ============================================================
    #region OperationResult<T> (AI-Friendly Result Model)
    // ============================================================
    public sealed class OperationResult<T>
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
        public T? Data { get; init; }

        public static OperationResult<T> Ok(T data) =>
            new() { Success = true, Data = data };

        public static OperationResult<T> Fail(string message) =>
            new() { Success = false, Error = message };
    }
    #endregion
}