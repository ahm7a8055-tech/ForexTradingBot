using System.Collections.Generic;

namespace Application.DTOs.Diagnostics
{
    #region ConnectivityStatusDto
    /// <summary>
    /// Represents the overall health and connectivity status of the application's key dependencies.
    /// This DTO is typically used for a health check endpoint to provide a snapshot of the system's operational readiness.
    /// </summary>
    public class ConnectivityStatusDto
    {
        #region Properties

        #region Database Status
        /// <summary>
        /// Gets or sets a value indicating whether a successful connection to the database could be established.
        /// </summary>
        /// <example>true</example>
        public bool CanConnectToDatabase { get; set; }

        /// <summary>
        /// Gets or sets the error message if the database connection failed.
        /// This will be null if <see cref="CanConnectToDatabase"/> is true.
        /// </summary>
        /// <example>A network-related or instance-specific error occurred while establishing a connection to SQL Server.</example>
        public string? DatabaseError { get; set; }

        /// <summary>
        /// Gets or sets the name of the database provider being used (e.g., PostgreSQL, SQLite).
        /// This provides context for which database is being checked.
        /// </summary>
        /// <example>PostgreSQL</example>
        public string? DatabaseProvider { get; set; }
        #endregion

        #region Telegram API Status
        /// <summary>
        /// Gets or sets a value indicating whether a successful connection to the Telegram Bot API could be established.
        /// </summary>
        /// <example>true</example>
        public bool CanAccessTelegramApi { get; set; }

        /// <summary>
        /// Gets or sets the error message if the Telegram API connection failed.
        /// This will be null if <see cref="CanAccessTelegramApi"/> is true.
        /// </summary>
        /// <example>Unauthorized: Bot token is invalid.</example>
        public string? TelegramApiError { get; set; }

        /// <summary>
        /// Gets or sets the username of the bot that was successfully connected to.
        /// This helps confirm that the correct bot token is being used.
        /// </summary>
        /// <example>@MyAwesomeBot</example>
        public string? TelegramBotUsername { get; set; }
        #endregion

        #region General Information
        /// <summary>
        /// Gets or sets a list of general informational messages, warnings, or other diagnostic details.
        /// </summary>
        /// <example>["Configuration loaded successfully.", "Cache service is responsive."]</example>
        public List<string> Messages { get; set; } = new();
        #endregion

        #endregion
    }
    #endregion
}