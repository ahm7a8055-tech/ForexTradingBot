// File: TelegramPanel/Application/CommandHandlers/MenuCommandHandler.cs
#region Usings
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramPanel.Application.CommandHandlers.Features.CoinGecko;
using TelegramPanel.Application.Interfaces;
using TelegramPanel.Formatters;
using TelegramPanel.Infrastructure.Helper;
using static TelegramPanel.Application.CommandHandlers.Features.CoinGecko.CryptoCallbackHandler;
using static TelegramPanel.Infrastructure.ActualTelegramMessageActions;
#endregion

namespace TelegramPanel.Application.CommandHandlers.MainMenu
{
    public class MenuCommandHandler : ITelegramCommandHandler
    {
        #region Private Fields
        private readonly ILogger<MenuCommandHandler> _logger;
        private readonly ITelegramMessageSender _messageSender;

        // Callback Data constants for menu buttons
        private readonly IServiceProvider _serviceProvider;
        public const string SignalsCallbackData = "menu_view_signals";
        public const string ProfileCallbackData = "menu_my_profile";
        public const string SubscribeCallbackData = "menu_subscribe_plans";
        public const string SettingsCallbackData = "menu_user_settings";
        public const string MarketAnalysisData = "market_analysis";
        public const string AnalysisCallbackData = "menu_analysis";
        public const string EconomicCalendarCallbackData = "menu_econ_calendar"; // <<< NEW
        public const string CryptoCallbackData = "menu_crypto_details"; // Kept for the old FMP feature if you ever enable it
        public const string CoingeckoCallbackData = "menu_coingecko_trending"; // Using the constant from the handler itself
        public const string BackToMainMenuGeneral = "back_to_main_menu";
        private readonly IMemoryCacheService<UiCacheEntry> _uiCache; // <-- NEW
        public const string CloudflareRadarCallbackData = "menu_cf_radar";
        private const string MainMenuCacheKey = "MainMenu_v1";
        #endregion

        #region Constructor
        public MenuCommandHandler(ILogger<MenuCommandHandler> logger, ITelegramMessageSender messageSender, IMemoryCacheService<UiCacheEntry> uiCache , IServiceProvider serviceProvider)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _uiCache = uiCache ?? throw new ArgumentNullException(nameof(uiCache)); // <-- NEW
            _messageSender = messageSender ?? throw new ArgumentNullException(nameof(messageSender));
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Generates the main menu markup with buttons for various features.
        /// </summary>
        /// <returns></returns>
        public static (string text, InlineKeyboardMarkup keyboard) GetMainMenuMarkup()
        {
            StringBuilder text = new StringBuilder()

               // Header
               .AppendLine(TelegramMessageFormatter.Bold("👋 Welcome to Your Trading Bot!").Replace("\\.", ""))
                .AppendLine(TelegramMessageFormatter.Italic("Your comprehensive tool for market insights and signals.").Replace("\\.", ""))
                .AppendLine() // Blank line

                // Navigation Hint
                .AppendLine("Choose an option from the sections below:").Replace("\\.", "")
                .AppendLine(TelegramMessageFormatter.Italic("Tap on buttons to explore features or manage your account.").Replace("\\.", ""))
                .AppendLine() // Blank line before first section

                // Section 1: Trading Essentials
                // Combined button details for this section onto one line with \n
                .AppendLine("📈 " + TelegramMessageFormatter.Bold("View Signals") + ": " + TelegramMessageFormatter.Italic("See the latest trading signals recommended by our analysis.").Replace("\\.", ""))
                .AppendLine("📊 " + TelegramMessageFormatter.Bold("Market Analysis") + ": " + TelegramMessageFormatter.Italic("Get an overview of current market conditions and trends.").Replace("\\.", ""))
                .AppendLine("📰 " + TelegramMessageFormatter.Bold("News Analysis") + ": " + TelegramMessageFormatter.Italic("Explore recent news impacting the markets and sentiment.").Replace("\\.", ""))
                .AppendLine("🗓️ " + TelegramMessageFormatter.Bold("Economic Calendar") + ": " + TelegramMessageFormatter.Italic("Stay informed about important upcoming economic events.").Replace("\\.", ""))
                .AppendLine("📈 " + TelegramMessageFormatter.Bold("Crypto Prices") + ": " + TelegramMessageFormatter.Italic("View real-time market data for popular cryptocurrencies.").Replace("\\.", ""))
                .AppendLine("✨ " + TelegramMessageFormatter.Bold("View Plans") + ": " + TelegramMessageFormatter.Italic("Learn about subscription tiers and unlock premium features.").Replace("\\.", ""))
                .AppendLine("☁️ " + TelegramMessageFormatter.Bold("Cloudflare Radar") + ": " + TelegramMessageFormatter.Italic("Check global internet health and trends.").Replace("\\.", ""))
                .AppendLine("⚙️ " + TelegramMessageFormatter.Bold("Settings") + ": " + TelegramMessageFormatter.Italic("Manage your notification preferences and other bot settings.").Replace("\\.", ""))
                .AppendLine("👤 " + TelegramMessageFormatter.Bold("My Profile") + ": " + TelegramMessageFormatter.Italic("View your account status, subscription details, and history.").Replace("\\.", ""))
                .AppendLine();


            InlineKeyboardMarkup? keyboard = MarkupBuilder.CreateInlineKeyboard(
                new[] // Row 1: Core Features
                {
                   InlineKeyboardButton.WithCallbackData("📈 View Signals", SignalsCallbackData),
                   InlineKeyboardButton.WithCallbackData("📊 Market Analysis", MarketAnalysisData)
                },
                new[] // Row 2: NEW Analysis Button
                {
                   InlineKeyboardButton.WithCallbackData("🔍 News Analysis", AnalysisCallbackData),
                   InlineKeyboardButton.WithCallbackData("🗓️ Economic Calendar", EconomicCalendarCallbackData),
                   InlineKeyboardButton.WithCallbackData("☁️ Cloudflare Radar", CloudflareRadarCallbackData)
                },
                new[] // Row 3: Crypto Details (NEW)
                {
                   InlineKeyboardButton.WithCallbackData("🪙 Crypto Prices", $"{CryptoCallbackHandler.CallbackPrefix}_list_1")
                },
                new[] // Row 3: Subscription
                {
                   InlineKeyboardButton.WithCallbackData("💎 Subscribe / Plans", SubscribeCallbackData)
                },
                new[]
            {
           InlineKeyboardButton.WithCallbackData("📚 Learn & Grow", "edu_main")
              },
                new[] // Row 4: Account Management
                {
                   InlineKeyboardButton.WithCallbackData("⚙️ Settings", SettingsCallbackData),
                   InlineKeyboardButton.WithCallbackData("👤 My Profile", ProfileCallbackData)
                }
            );

            // Ensure the keyboard is not null to match the expected return type.
            return keyboard == null
                ? throw new InvalidOperationException("Keyboard generation failed.")
                : (text: text.ToString(), keyboard);
        }

        #endregion

        #region ITelegramCommandHandler Implementation
        public bool CanHandle(Update update)
        {
            // THIS IS THE CORRECTED LOGIC FOR A COMMAND HANDLER
            return update.Type == UpdateType.Message &&
                   update.Message?.Text?.Trim().Equals("/menu", StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Handles the request to view the main menu triggered by a message (e.g., /menu command).
        /// </summary>
        /// <param name="update">The update containing the message.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task HandleAsync(Update update, CancellationToken cancellationToken = default)
        {
            // This logic correctly handles the /menu command by sending the menu.
            Message? message = update.Message;

            if (message == null)
            {
                _logger.LogWarning("MenuCommand: Message is null in UpdateID {UpdateId}, despite CanHandle passing.", update.Id);
                return;
            }

            long chatId = message.Chat.Id;
            long? userId = message.From?.Id;

            _logger.LogInformation("Handling /menu command for ChatID {ChatId}, UserID {UserId}", chatId, userId);

            try
            {
                // --- Caching Logic ---
                if (!_uiCache.TryGetValue(MainMenuCacheKey, out UiCacheEntry? cachedMenu) || cachedMenu == null)
                {
                    _logger.LogInformation("Main menu cache MISS. Generating and caching menu.");
                    (string text, InlineKeyboardMarkup keyboard) = GetMainMenuMarkup();
                    cachedMenu = new UiCacheEntry(text, keyboard);
                    _uiCache.Set(MainMenuCacheKey, cachedMenu, TimeSpan.FromHours(5));
                }
                else
                {
                    _logger.LogInformation("Main menu cache HIT. Serving from cache.");
                }

                // --- Send the Main Menu ---
                await _messageSender.SendTextMessageAsync(
                    chatId: chatId,
                    text: cachedMenu.Text,
                    parseMode: ParseMode.MarkdownV2,
                    replyMarkup: cachedMenu.Keyboard,
                    cancellationToken: cancellationToken);

                _logger.LogDebug("Main menu sent successfully to ChatID {ChatId} via /menu command.", chatId);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                // This is a controlled cancellation, not an error.
                _logger.LogInformation(ex, "Sending main menu to ChatID {ChatId} was cancelled.", chatId);
            }
            catch (Exception ex)
            {
                // This is the upgraded block for unexpected errors.
                _logger.LogError(ex, "An unexpected error occurred while sending the main menu to ChatID {ChatId}, UserID {UserId}", chatId, userId);

                // --- ENHANCEMENT: Log the failure to the database in a background task ---
                _ = Task.Run(async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    // We resolve the repository here to avoid making it a direct dependency of this handler.
                    var repo = scope.ServiceProvider.GetRequiredService<IProMonitoringLogRepository>();
                    await repo.AddAsync(new ProMonitoringLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "Error",
                        Source = "MenuCommandHandler",
                        EventType = "MenuCommandSend",
                        Message = $"Failed to send main menu for user {userId}.",
                        Details = ex.StackTrace,
                        Exception = ex.ToString(),
                        Status = "Failed",
                        CreatedAt = DateTime.UtcNow
                    });
                });
                // --- END OF ENHANCEMENT ---

                // --- Send a safe fallback error message to the user ---
                try
                {
                    await _messageSender.SendTextMessageAsync(
                        chatId: chatId,
                        text: "❌ An unexpected error occurred while trying to show the menu. Our team has been notified. Please try again in a moment.",
                        cancellationToken: cancellationToken);
                }
                catch (Exception sendErrorEx)
                {
                    // Log if even the error message fails, but don't throw further.
                    _logger.LogError(sendErrorEx, "CRITICAL: Failed to send fallback error message to ChatID {ChatId} after menu command failure.", chatId);
                }
            }
        }
        #endregion

    }
}