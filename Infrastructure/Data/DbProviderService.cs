// File: Infrastructure/Data/DbProviderService.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data
{
    /// <summary>
    /// A singleton service that determines the configured database provider once at startup.
    /// This prevents multiple components from re-parsing the configuration.
    /// </summary>
    public class DbProviderService
    {
        /// <summary>
        /// Gets the configured database provider.
        /// </summary>
        public DatabaseProvider Provider { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DbProviderService"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="logger">The logger for this service.</param>
        public DbProviderService(IConfiguration configuration, ILogger<DbProviderService> logger)
        {
            // Retrieves the database provider name from the configuration, converting it to lowercase for case-insensitive comparison.
            string? providerName = configuration.GetValue<string>("DatabaseSettings:DatabaseProvider")?.ToLowerInvariant();

            // If the provider name is not specified in the configuration, log a warning and default to SQLite.
            if (string.IsNullOrEmpty(providerName))
            {
                logger.LogWarning("DatabaseSettings:DatabaseProvider not specified in configuration. Defaulting to SQLite.");
                providerName = "sqlite";
            }

            // Determines the DatabaseProvider enum value based on the provider name.
            switch (providerName)
            {
                case "postgres":
                case "postgresql": // Supports both 'postgres' and 'postgresql' as valid names.
                    Provider = DatabaseProvider.Postgres;
                    break;
                case "sqlite":
                    Provider = DatabaseProvider.SQLite;
                    break;
                case "sqlserver":
                    Provider = DatabaseProvider.SqlServer;
                    break;
                default: // If the provider name is not recognized, set it to Unsupported and log an error.
                    Provider = DatabaseProvider.Unsupported;
                    logger.LogError("Unsupported DatabaseProvider '{ProviderName}' configured.", providerName);
                    break;
            }

            // Logs the determined database provider for informational purposes.
            logger.LogInformation("Database provider configured as: {Provider}", Provider);
        }
    }
}