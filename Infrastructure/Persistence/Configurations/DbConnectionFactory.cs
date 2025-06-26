using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Persistence;

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly string _dbProvider;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _dbProvider = configuration.GetValue<string>("DatabaseSettings:DatabaseProvider")?.ToLowerInvariant();
        _connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(_dbProvider) || string.IsNullOrEmpty(_connectionString))
        {
            throw new InvalidOperationException("Database provider or connection string is not configured properly.");
        }
    }

    public IDbConnection CreateConnection()
    {
        return _dbProvider switch
        {
            "postgres" or "postgresql" => new NpgsqlConnection(_connectionString),
            "sqlserver" => new SqlConnection(_connectionString),
            _ => throw new NotSupportedException($"Database provider '{_dbProvider}' is not supported.")
        };
    }
}