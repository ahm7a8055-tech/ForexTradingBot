using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices; // Added for OS checks if needed

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
                // --- STRONG FIX: Automatic Permission Handling ---
                string storageDir = basePath;

                // 1. Verify write permissions for the requested path
                if (!IsDirectoryWritable(storageDir))
                {
                    string tempPath = Path.GetTempPath();
                    _logger?.LogWarning("⚠️ Write permission denied for '{BasePath}'. Falling back to system temp directory: '{TempPath}' for EasySetup database.", basePath, tempPath);
                    storageDir = tempPath;
                }

                // 2. Ensure the directory exists
                if (!Directory.Exists(storageDir))
                {
                    try
                    {
                        Directory.CreateDirectory(storageDir);
                    }
                    catch (Exception dirEx)
                    {
                        _logger?.LogError(dirEx, "Failed to create directory '{StorageDir}'.", storageDir);
                        // Fallback to temp if creation fails
                        storageDir = Path.GetTempPath();
                    }
                }

                var dbPath = Path.Combine(storageDir, "easysetup_config.db");
                // ------------------------------------------------

                // Use ReadWriteCreate mode
                _connectionString = $"Data Source={dbPath};Cache=Shared;Mode=ReadWriteCreate";

                _logger?.LogInformation("Initializing EasySetupConfigStore at path: {DbPath}", dbPath);

                EnsureSchema();
            }
            catch (Exception ex)
            {
                // CRITICAL FIX: Never crash the app here. Fallback to In-Memory.
                _logger?.LogError(ex, "Failed to initialize persistent EasySetupConfigStore. Switching to In-Memory mode (non-persistent).");

                _connectionString = "Data Source=:memory:;Mode=Memory;Cache=Shared";
                try
                {
                    EnsureSchema();
                    _logger?.LogWarning("EasySetupConfigStore is running in In-Memory mode.");
                }
                catch
                {
                    // If even memory fails, we suppress it to allow the app to start
                    _logger?.LogCritical("EasySetupConfigStore failed to initialize even in memory.");
                }
            }
        }

        /// <summary>
        /// Helper method to safely check if the application can write to a directory.
        /// </summary>
        private bool IsDirectoryWritable(string dirPath)
        {
            try
            {
                if (!Directory.Exists(dirPath)) return false; // Can't write if it doesn't exist (and we assume we can't create it if checking permissions)

                // Try to create a zero-byte temporary file
                string testFile = Path.Combine(dirPath, $".write_test_{Guid.NewGuid()}");
                using (File.Create(testFile, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        // ============================================================
        #region Schema Initialization
        // ============================================================
        private void EnsureSchema()
        {
            // Wrap in try-catch to ensure connection issues don't propagate
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // Only try WAL mode if not in memory
                if (!_connectionString.Contains(":memory:"))
                {
                    try
                    {
                        using var walCmd = connection.CreateCommand();
                        walCmd.CommandText = "PRAGMA journal_mode = WAL;";
                        walCmd.ExecuteNonQuery();
                        _logger?.LogInformation("SQLite journal mode set to WAL.");
                    }
                    catch
                    {
                        _logger?.LogWarning("Failed to set WAL mode. Ignoring.");
                    }
                }

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

                _logger?.LogInformation("EasySetupConfigStore schema ensured successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Schema initialization failed for connection: {ConnectionString}", _connectionString);
                throw; // Re-throw to be caught by the Constructor's fallback logic
            }
        }
        #endregion

        // ... (LoadAll, Save, and OperationResult methods remain unchanged) ...

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
            }
            catch (Exception ex)
            {
                // If the DB is broken, just return empty instead of crashing
                _logger?.LogError(ex, "Error reading from EasySetupConfigStore. Returning empty configuration.");
                return result;
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
                return OperationResult<bool>.Fail($"Save failed: {ex.Message}");
            }
        }
        #endregion

        // ============================================================
        #region Save (Raw)
        // ============================================================
        public void Save(string key, string? value, bool isSensitive)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

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
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error writing to EasySetupConfigStore for key {Key}", key);
                // Don't throw, just log
            }
        }
        #endregion
    }

    // ============================================================
    #region OperationResult<T>
    // ============================================================
    /// <summary>
    /// Represents a standardized result model for operations that may succeed or fail.
    /// Provides a consistent contract for returning data, errors, and success flags.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the payload returned when the operation succeeds.
    /// </typeparam>
    /// <remarks>
    /// This pattern is commonly used for API responses, service-layer results,
    /// and AI-friendly output formats. It ensures predictable structure and avoids
    /// exceptions for expected operational failures.
    /// </remarks>
    public sealed class OperationResult<T>
    {
        /// <summary>
        /// Indicates whether the operation completed successfully.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Contains an error message when the operation fails.
        /// Null when <see cref="Success"/> is true.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// Contains the returned data when the operation succeeds.
        /// Null when <see cref="Success"/> is false.
        /// </summary>
        public T? Data { get; init; }

        /// <summary>
        /// Creates a successful <see cref="OperationResult{T}"/> with the specified data.
        /// </summary>
        /// <param name="data">The payload returned by the operation.</param>
        /// <returns>A successful result object.</returns>
        public static OperationResult<T> Ok(T data) =>
            new() { Success = true, Data = data };

        /// <summary>
        /// Creates a failed <see cref="OperationResult{T}"/> with an error message.
        /// </summary>
        /// <param name="message">A human-readable error description.</param>
        /// <returns>A failed result object containing the error.</returns>
        public static OperationResult<T> Fail(string message) =>
            new() { Success = false, Error = message };
    }
    #endregion
}