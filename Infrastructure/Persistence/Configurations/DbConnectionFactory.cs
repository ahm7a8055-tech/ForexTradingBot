// File: Infrastructure/Persistence/DbConnectionFactory.cs (Or wherever it is located)
using Infrastructure.Data;
using Application.Common.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;
using System.Data.SqlClient; // Assuming you might use SQL Server

namespace Infrastructure.Persistence.Configurations
{
    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly Data.DbProviderService _providerService;
        private readonly string _connectionString;

        public DbConnectionFactory(IConfiguration configuration, Data.DbProviderService providerService)
        {
            _providerService = providerService;

            // --- IMPROVED: Don't auto-default to SQLite, require proper configuration ---
            // Get the connection string from configuration.
            string? connectionStringFromConfig = configuration.GetConnectionString("DefaultConnection");

            // If it's missing, throw an exception instead of auto-defaulting
            if (string.IsNullOrEmpty(connectionStringFromConfig))
            {
                throw new InvalidOperationException(
                    "DbConnectionFactory: DefaultConnection not found in configuration. " +
                    "The application should prompt the user for database connection details in Program.cs before reaching this point.");
            }

            _connectionString = connectionStringFromConfig;
        }

        public IDbConnection CreateConnection()
        {
            // This logic can now reliably use the _connectionString and _providerService fields.
            return _providerService.Provider switch
            {
                DatabaseProvider.Postgres => new NpgsqlConnection(_connectionString),
                DatabaseProvider.SQLite => new SqliteConnection(_connectionString),
                DatabaseProvider.SqlServer => new SqlConnection(_connectionString),
                _ => throw new NotSupportedException($"Database provider '{_providerService.Provider}' is not supported for connection creation.")
            };
        }
    }
}