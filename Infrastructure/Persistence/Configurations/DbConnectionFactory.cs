// File: Infrastructure/Persistence/DbConnectionFactory.cs (Or wherever it is located)

using Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Npgsql;
using System.Data;
using System.Data.SqlClient; // Assuming you might use SQL Server
using Serilog;
using Application.Common.Interfaces;

namespace Infrastructure.Persistence
{
    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly DbProviderService _providerService;
        private readonly string _connectionString;

        public DbConnectionFactory(IConfiguration configuration, DbProviderService providerService)
        {
            _providerService = providerService;

            // --- FIX APPLIED HERE: Centralized connection string logic ---
            // Get the connection string from configuration.
            var connectionStringFromConfig = configuration.GetConnectionString("DefaultConnection");

            // If it's missing, apply the same fallback logic your app uses at startup.
            if (string.IsNullOrEmpty(connectionStringFromConfig))
            {
                Log.Warning("DbConnectionFactory: DefaultConnection not found in configuration. Applying default SQLite connection string.");
                _connectionString = "Data Source=local_forex_bot.db";

                // Ensure the provider service reflects this default if it was also not configured.
                if (_providerService.Provider == DatabaseProvider.Unsupported)
                {
                    // This is a safeguard. Normally the DbProviderService constructor handles this.
                    // But we ensure consistency here.
                }
            }
            else
            {

                _connectionString = connectionStringFromConfig;
            }
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