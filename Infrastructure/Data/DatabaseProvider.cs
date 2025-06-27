// File: Infrastructure/Data/DatabaseProvider.cs
namespace Infrastructure.Data
{
    /// <summary>
    /// Represents the supported database providers for the application.
    /// </summary>
    public enum DatabaseProvider
    {
        /// <summary>
        /// Indicates that the specified database provider is not supported.
        /// </summary>
        Unsupported,

        /// <summary>
        /// Represents the PostgreSQL database provider.
        /// </summary>
        Postgres,

        /// <summary>
        /// Represents the SQLite database provider.
        /// </summary>
        SQLite,

        /// <summary>
        /// Represents the SQL Server database provider.
        /// </summary>
        SqlServer
    }
}