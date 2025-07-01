using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using System.Data;

namespace Infrastructure.Services
{
    public interface IHangfireCleaner
    {
        Task PurgeCompletedAndFailedJobsOlderThanAsync(string? connectionString, TimeSpan olderThan, CancellationToken cancellationToken = default);
    }

    public class HangfireCleaner : IHangfireCleaner
    {
        public async Task PurgeCompletedAndFailedJobsOlderThanAsync(string? connectionString, TimeSpan olderThan, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            }

            string lowerConn = connectionString.ToLowerInvariant();
            DateTime cutoff = DateTime.UtcNow - olderThan;

            if (lowerConn.Contains("server=") || (lowerConn.Contains("data source=") && !lowerConn.Contains(".db")))
            {
                // SQL Server
                using SqlConnection conn = new(connectionString);
                await conn.OpenAsync(cancellationToken);
                await PurgeSqlServer(conn, cutoff, cancellationToken);
            }
            else if (lowerConn.Contains("host=") || lowerConn.Contains("postgres"))
            {
                // PostgreSQL
                using NpgsqlConnection conn = new(connectionString);
                await conn.OpenAsync(cancellationToken);
                await PurgePostgres(conn, cutoff, cancellationToken);
            }
            else if (lowerConn.Contains(".db") || lowerConn.Contains("sqlite"))
            {
                // SQLite
                using SqliteConnection conn = new(connectionString);
                await conn.OpenAsync(cancellationToken);
                await PurgeSqlite(conn, cutoff, cancellationToken);
            }
            else
            {
                throw new NotSupportedException($"Unknown or unsupported DB provider for connection string: {connectionString}");
            }
        }

        private static async Task PurgeSqlServer(IDbConnection conn, DateTime cutoff, CancellationToken cancellationToken)
        {
            string sql = @"
                DELETE FROM [HangFire].[State] WHERE [JobId] IN (
                    SELECT [Id] FROM [HangFire].[Job] WHERE ([StateName] = 'Succeeded' OR [StateName] = 'Failed') AND [CreatedAt] < @Cutoff
                );
                DELETE FROM [HangFire].[Job] WHERE ([StateName] = 'Succeeded' OR [StateName] = 'Failed') AND [CreatedAt] < @Cutoff;
            ";
            _ = await conn.ExecuteAsync(new CommandDefinition(sql, new { Cutoff = cutoff }, cancellationToken: cancellationToken));
        }

        private static async Task PurgePostgres(IDbConnection conn, DateTime cutoff, CancellationToken cancellationToken)
        {
            string sql = @"
                DELETE FROM ""HangFire"".""State"" WHERE ""JobId"" IN (
                    SELECT ""Id"" FROM ""HangFire"".""Job"" WHERE (""StateName"" = 'Succeeded' OR ""StateName"" = 'Failed') AND ""CreatedAt"" < @Cutoff
                );
                DELETE FROM ""HangFire"".""Job"" WHERE (""StateName"" = 'Succeeded' OR ""StateName"" = 'Failed') AND ""CreatedAt"" < @Cutoff;
            ";
            _ = await conn.ExecuteAsync(new CommandDefinition(sql, new { Cutoff = cutoff }, cancellationToken: cancellationToken));
        }

        private static async Task PurgeSqlite(IDbConnection conn, DateTime cutoff, CancellationToken cancellationToken)
        {
            string sql = @"
                DELETE FROM State WHERE JobId IN (
                    SELECT Id FROM Job WHERE (StateName = 'Succeeded' OR StateName = 'Failed') AND CreatedAt < @Cutoff
                );
                DELETE FROM Job WHERE (StateName = 'Succeeded' OR StateName = 'Failed') AND CreatedAt < @Cutoff;
            ";
            _ = await conn.ExecuteAsync(new CommandDefinition(sql, new { Cutoff = cutoff }, cancellationToken: cancellationToken));
        }
    }
}