using System.Data;

namespace Application.Common.Interfaces;

/// <summary>
/// A factory responsible for creating a database connection based on the configured provider.
/// This centralizes the logic for choosing between SQL Server, PostgreSQL, etc.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates and returns a new IDbConnection instance.
    /// The caller is responsible for opening, closing, and disposing of the connection.
    /// </summary>
    /// <returns>A new instance of IDbConnection (e.g., SqlConnection or NpgsqlConnection).</returns>
    IDbConnection CreateConnection();
}