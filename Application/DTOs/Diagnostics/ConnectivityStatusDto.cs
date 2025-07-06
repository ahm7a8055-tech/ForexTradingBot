using System.Collections.Generic;

namespace Application.DTOs.Diagnostics
{
    public class ConnectivityStatusDto
    {
        public bool CanConnectToDatabase { get; set; }
        public string? DatabaseError { get; set; }
        public string? DatabaseProvider { get; set; } // e.g., PostgreSQL, SQLite

        public bool CanAccessTelegramApi { get; set; }
        public string? TelegramApiError { get; set; }
        public string? TelegramBotUsername { get; set; } // To confirm which bot we connected to

        public List<string> Messages { get; set; } = new List<string>(); // General messages or details
    }
}
