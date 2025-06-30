// File: WebAPI/Setup/ConfigurationValidator.cs (NEW FILE)

namespace WebAPI.Setup;

/// <summary>
/// A non-interactive helper class to validate application configuration on startup.
/// If any validation fails, it throws a clear exception to stop the application.
/// </summary>
public static class ConfigurationValidator
{
    public static void Validate(IConfiguration configuration)
    {
        // Validate Database Connection
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("FATAL: Required configuration 'ConnectionStrings:DefaultConnection' is missing.");
        }

        // Validate Telegram Bot Token
        var botToken = configuration.GetValue<string>("TelegramPanel:BotToken");
        if (string.IsNullOrWhiteSpace(botToken) || botToken.Contains("REPLACE"))
        {
            throw new InvalidOperationException("FATAL: Required configuration 'TelegramPanel:BotToken' is missing or has a placeholder value.");
        }

        // Add any other critical validation checks here.
    }
}