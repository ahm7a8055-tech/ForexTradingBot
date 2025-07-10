using Application.Common.Interfaces;
using Dapper;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Retry;
using System.Data;
using System.Data.Common;

namespace Infrastructure.Persistence.Repositories // Ensure namespace matches your project structure
{
    /// <summary>
    /// Implements IAiApiConfigurationRepository using Dapper for efficient PostgreSQL data access.
    /// This implementation mirrors the architectural pattern of NewsItemRepository, using direct IConfiguration
    /// injection and a private connection creation method.
    /// </summary>
    public class AiApiConfigurationRepository : IAiApiConfigurationRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<AiApiConfigurationRepository> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private const int CommandTimeoutSeconds = 30;

        private const string TableName = "\"AiApiConfigurations\"";
        // --- FIX: Added "ApiKeyName" to the SELECT statement ---
        private const string BaseSelectSql = $@"
            SELECT
                ""Id"", ""ProviderName"", ""IsEnabled"", ""ApiKey"", ""ModelName"", ""PromptTemplate"",
                ""Description"", ""CreatedAt"", ""LastUpdatedAt"", ""ApiKeyName""
            FROM public.{TableName}";

        /// <summary>
        /// Initializes a new instance of the AiApiConfigurationRepository class.
        /// </summary>
        /// <param name="configuration">The application's configuration, used to retrieve the database connection string.</param>
        /// <param name="logger">The logger instance for recording operational events and errors.</param>
        public AiApiConfigurationRepository(IConfiguration configuration, ILogger<AiApiConfigurationRepository> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new InvalidOperationException("DefaultConnection string not found in configuration.");

            // Polly configuration for transient errors, adapted for PostgreSQL.
            // It will not retry on unique constraint violations (SQLSTATE 23505).
            _retryPolicy = Policy
                .Handle<DbException>(ex => !(ex is PostgresException pgEx && pgEx.SqlState == "23505"))
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryAttempt, context) =>
                    {
                        _logger.LogWarning(exception,
                            "AiApiConfigurationRepository: Transient database error encountered. Retrying in {TimeSpan} for attempt {RetryAttempt}. Error: {Message}",
                            timeSpan, retryAttempt, exception.Message);
                    });
        }

        /// <summary>
        /// Creates and returns a new instance of DbConnection using the configured connection string.
        /// </summary>
        private DbConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        public async Task<AiApiConfiguration?> GetByProviderAndStatusAsync(string providerName, bool isEnabled, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(providerName)) return null;
            string sql = $@"{BaseSelectSql} WHERE ""ProviderName"" = @ProviderName AND ""IsEnabled"" = @IsEnabled;";

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    await using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    return await connection.QuerySingleOrDefaultAsync<AiApiConfiguration>(
                        new CommandDefinition(sql, new { ProviderName = providerName, IsEnabled = isEnabled }, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken)
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get AiApiConfiguration by Provider '{ProviderName}' and IsEnabled '{IsEnabled}'.", providerName, isEnabled);
                throw new RepositoryException($"Failed to get configuration for provider '{providerName}' with status '{isEnabled}'.", ex);
            }
        }

        public async Task<AiApiConfiguration?> GetByProviderNameAsync(string providerName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(providerName)) return null;
            string sql = $@"{BaseSelectSql} WHERE ""ProviderName"" = @ProviderName;";

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    await using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    return await connection.QuerySingleOrDefaultAsync<AiApiConfiguration>(
                        new CommandDefinition(sql, new { ProviderName = providerName }, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken)
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get AiApiConfiguration by Provider '{ProviderName}'.", providerName);
                throw new RepositoryException($"Failed to get configuration for provider '{providerName}'.", ex);
            }
        }

        public async Task<IEnumerable<AiApiConfiguration>> GetAllEnabledAsync(CancellationToken cancellationToken)
        {
            string sql = $@"{BaseSelectSql} WHERE ""IsEnabled"" = true;";
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    await using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    return await connection.QueryAsync<AiApiConfiguration>(
                        new CommandDefinition(sql, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken)
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch all enabled AiApiConfigurations.");
                throw new RepositoryException("Failed to fetch all enabled configurations.", ex);
            }
        }

        public async Task<IEnumerable<AiApiConfiguration>> GetAllByProviderAndStatusAsync(string providerName, bool isEnabled, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(providerName)) return Enumerable.Empty<AiApiConfiguration>();
            string sql = $@"{BaseSelectSql} WHERE ""ProviderName"" = @ProviderName AND ""IsEnabled"" = @IsEnabled;";

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    await using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    return await connection.QueryAsync<AiApiConfiguration>(
                        new CommandDefinition(sql, new { ProviderName = providerName, IsEnabled = isEnabled }, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken)
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all AiApiConfigurations by Provider '{ProviderName}' and IsEnabled '{IsEnabled}'.", providerName, isEnabled);
                throw new RepositoryException($"Failed to get all configurations for provider '{providerName}' with status '{isEnabled}'.", ex);
            }
        }

        public async Task<IEnumerable<AiApiConfiguration>> GetAllByProviderAndStatusAndKeyNameAsync(
            string providerName,
            bool isEnabled,
            string? apiKeyName = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                _logger.LogWarning("ProviderName cannot be null or empty.");
                return Enumerable.Empty<AiApiConfiguration>();
            }

            // This block is functionally correct, its issue was the underlying BaseSelectSql.
            // No changes are needed here now that BaseSelectSql is fixed.
            string baseSql = BaseSelectSql;
            string sql = $"{baseSql} WHERE \"ProviderName\" = @ProviderName AND \"IsEnabled\" = @IsEnabled";
            if (!string.IsNullOrWhiteSpace(apiKeyName))
            {
                sql += " AND \"ApiKeyName\" = @ApiKeyName";
            }
            sql += ";";

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    await using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    var parameters = new
                    {
                        ProviderName = providerName,
                        IsEnabled = isEnabled,
                        ApiKeyName = apiKeyName
                    };
                    return await connection.QueryAsync<AiApiConfiguration>(
                        new CommandDefinition(sql, parameters, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken)
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all AiApiConfigurations by Provider '{ProviderName}', IsEnabled '{IsEnabled}', and ApiKeyName '{ApiKeyName}'.", providerName, isEnabled, apiKeyName);
                throw new RepositoryException($"Failed to get all configurations for provider '{providerName}' with status '{isEnabled}' and ApiKeyName '{apiKeyName}'.", ex);
            }
        }

        public async Task<AiApiConfiguration> AddAsync(AiApiConfiguration configuration, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            // --- FIX: Added "ApiKeyName" to the INSERT statement ---
            string sql = $@"
                INSERT INTO public.{TableName} (""ProviderName"", ""IsEnabled"", ""ApiKey"", ""ModelName"", ""PromptTemplate"", ""Description"", ""ApiKeyName"")
                VALUES (@ProviderName, @IsEnabled, @ApiKey, @ModelName, @PromptTemplate, @Description, @ApiKeyName)
                RETURNING *;";

            try
            {
                var addedConfig = await _retryPolicy.ExecuteAsync(async () =>
                {
                    await using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    return await connection.QuerySingleAsync<AiApiConfiguration>(
                        new CommandDefinition(sql, configuration, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken)
                    );
                });

                _logger.LogInformation("Successfully added AiApiConfiguration with Id: {ConfigId} for Provider: '{ProviderName}'", addedConfig.Id, addedConfig.ProviderName);
                return addedConfig;
            }
            catch (PostgresException pEx) when (pEx.SqlState == "23505") // Assuming unique constraint on ProviderName or a similar field
            {
                _logger.LogError(pEx, "Failed to add AiApiConfiguration for Provider '{ProviderName}'. A configuration with this provider name already exists.", configuration.ProviderName);
                throw new RepositoryException($"A configuration for provider '{configuration.ProviderName}' already exists.", pEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add AiApiConfiguration for Provider '{ProviderName}'.", configuration.ProviderName);
                throw new RepositoryException($"Failed to add configuration for provider '{configuration.ProviderName}'.", ex);
            }
        }

        public async Task UpdateAsync(AiApiConfiguration configuration, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            // --- FIX: Added "ApiKeyName" to the UPDATE statement ---
            string sql = $@"
                UPDATE public.{TableName}
                SET ""ProviderName"" = @ProviderName, ""IsEnabled"" = @IsEnabled, ""ApiKey"" = @ApiKey, ""ModelName"" = @ModelName,
                    ""PromptTemplate"" = @PromptTemplate, ""Description"" = @Description, ""ApiKeyName"" = @ApiKeyName, ""LastUpdatedAt"" = NOW()
                WHERE ""Id"" = @Id;";

            try
            {
                var rowsAffected = await _retryPolicy.ExecuteAsync(async () =>
                {
                    await using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    return await connection.ExecuteAsync(
                        new CommandDefinition(sql, configuration, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken)
                    );
                });

                if (rowsAffected == 0)
                {
                    throw new RepositoryException($"Update failed: AiApiConfiguration with Id '{configuration.Id}' was not found.");
                }
                _logger.LogInformation("Successfully updated AiApiConfiguration with Id: {ConfigId}", configuration.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update AiApiConfiguration with Id: {ConfigId}.", configuration.Id);
                throw new RepositoryException($"Failed to update configuration with Id '{configuration.Id}'.", ex);
            }
        }

        public async Task DeleteAsync(int id, CancellationToken cancellationToken)
        {
            string sql = $"DELETE FROM public.{TableName} WHERE \"Id\" = @Id;";
            try
            {
                var rowsAffected = await _retryPolicy.ExecuteAsync(async () =>
                {
                    await using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    return await connection.ExecuteAsync(
                        new CommandDefinition(sql, new { Id = id }, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken)
                    );
                });

                if (rowsAffected == 0)
                {
                    throw new RepositoryException($"Delete failed: AiApiConfiguration with Id '{id}' not found.");
                }
                _logger.LogInformation("Successfully deleted AiApiConfiguration with Id: {ConfigId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete AiApiConfiguration with Id: {ConfigId}", id);
                throw new RepositoryException($"Failed to delete configuration with Id '{id}'.", ex);
            }
        }

        public async Task<bool> ExistsAsync(string providerName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(providerName)) return false;
            string sql = $"SELECT COUNT(1) FROM public.{TableName} WHERE \"ProviderName\" = @ProviderName;";

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    await using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    var count = await connection.ExecuteScalarAsync<int>(
                        new CommandDefinition(sql, new { ProviderName = providerName }, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken)
                    );
                    return count > 0;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check existence of AiApiConfiguration for Provider '{ProviderName}'.", providerName);
                throw new RepositoryException($"Failed to check existence for provider '{providerName}'.", ex);
            }
        }
    }

    /// <summary>
    /// Custom exception for repository-level errors.
    /// This should be defined in a shared location, but is included here for completeness.
    /// </summary>
    public class RepositoryException : Exception
    {
        public RepositoryException(string message) : base(message) { }
        public RepositoryException(string message, Exception innerException) : base(message, innerException) { }
    }
}