#region Usings
using Application.Common.Interfaces;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramPanel.Application.CommandHandlers.MainMenu;
using TelegramPanel.Application.Interfaces;
using TelegramPanel.Formatters;
using static TelegramPanel.Infrastructure.ActualTelegramMessageActions;
// Explicitly use an alias for the domain entity User to avoid conflicts
using DomainUser = Domain.Entities.User;
// Explicitly specify the Telegram.Bot.Types namespace for all its types
using TGBotTypes = Telegram.Bot.Types;
#endregion

namespace TelegramPanel.Application.CommandHandlers.Entry
{
    /// <summary>
    /// Handles the /start command with a focus on security, scalability, and resilience.
    /// It uses Redis for distributed locking to prevent race conditions and ensures all user input is sanitized.
    /// </summary>
    public class StartCommandHandler : ITelegramCommandHandler, ITelegramCallbackQueryHandler
    {
        private readonly ILogger<StartCommandHandler> _logger;
        private readonly ITelegramMessageSender _messageSender;
        private readonly ITelegramStateMachine? _stateMachine;
        private readonly ITelegramBotClient _botClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private const string ShowMainMenuCallback = "show_main_menu";
        private readonly ILoggingSanitizer _logSanitizer;
        private readonly IUserRepository _userRepository; // Add this field
        private readonly IAdviceService _adviceService;
        public StartCommandHandler(
            ILoggingSanitizer logSanitizer,
            ILogger<StartCommandHandler> logger,
            ITelegramMessageSender messageSender,
            IServiceScopeFactory scopeFactory,
            ITelegramBotClient botClient, IAdviceService adviceService, 
            IUserRepository userRepository, // <-- ADD THIS PARAMETER
            ITelegramStateMachine? stateMachine = null)
        {
            _logSanitizer = logSanitizer ?? throw new ArgumentNullException(nameof(logSanitizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _adviceService = adviceService ?? throw new ArgumentNullException(nameof(adviceService));
            _messageSender = messageSender ?? throw new ArgumentNullException(nameof(messageSender));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository)); // <-- ADD THIS ASSIGNMENT
            _stateMachine = stateMachine;
        }


        // --- FIX: Explicitly use Telegram.Bot.Types.Update ---
        public bool CanHandle(Update update)
        {
            return update.Type == UpdateType.Message &&
                update.Message?.Text?.Trim().Equals("/start", StringComparison.OrdinalIgnoreCase) == true
|| update.Type == UpdateType.CallbackQuery &&
                update.CallbackQuery?.Data == ShowMainMenuCallback;
        }


        // --- FIX: Explicitly use Telegram.Bot.Types.Update ---
        public async Task HandleAsync(TGBotTypes.Update update, CancellationToken cancellationToken = default)
        {
            switch (update)
            {
                case { Message: { From: { } user, Chat: { } chat } }:
                    await HandleStartCommand(user, chat.Id, cancellationToken);
                    break;
                case { CallbackQuery: { From: { } user, Message: { } message } }:
                    await HandleShowMainMenuCallback(update.CallbackQuery, user, message, cancellationToken);
                    break;
                default:
                    _logger.LogWarning("StartCommandHandler received an update it cannot handle. UpdateType: {UpdateType}", update.Type);
                    break;
            }
        }

        [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
        private async Task HandleStartCommand(TGBotTypes.User telegramUser, long chatId, CancellationToken cancellationToken)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ICacheService cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
            string registrationLockKey = $"lock:register:{telegramUser.Id}";

            string? lockToken = await cacheService.AcquireLockAsync(registrationLockKey, TimeSpan.FromSeconds(30));
            if (lockToken == null)
            {
                _logger.LogWarning("Registration for UserID {UserId} is already in progress (lock held). Ignoring duplicate /start request.", telegramUser.Id);
                return;
            }

            try
            {
                string sanitizedFirstName = _logSanitizer.Sanitize(telegramUser.FirstName);
                _logger.LogInformation("Lock acquired for UserID {UserId}. Sending initial welcome message to {SanitizedFirstName}.", telegramUser.Id, sanitizedFirstName);
                Message sentMessage = await SendInitialWelcomeMessageAsync(chatId, telegramUser.FirstName, cancellationToken);

                _ = Task.Run(() => ProcessUserRegistrationAsync(telegramUser, sentMessage.MessageId, registrationLockKey, lockToken, cancellationToken), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send initial welcome message to UserID {UserId}. Releasing lock.", telegramUser.Id);
                _ = await cacheService.ReleaseLockAsync(registrationLockKey, lockToken);
            }
        }


        // --- FIX: Explicitly use Telegram.Bot.Types.User ---
        private async Task ProcessUserRegistrationAsync(TGBotTypes.User telegramUser, int messageId, string lockKey, string lockToken, CancellationToken cancellationToken)
        {
            long userId = telegramUser.Id;

            // We create a new scope for this background task to ensure all services have the correct lifetime.
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IUserService userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            IUserRepository userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            ICacheService cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
            ITelegramMessageSender messageSender = scope.ServiceProvider.GetRequiredService<ITelegramMessageSender>();
            ILogger<StartCommandHandler> scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<StartCommandHandler>>();
            string sanitizedUsernameForLogs = _logSanitizer.Sanitize(telegramUser.Username ?? telegramUser.FirstName);

            try
            {
                DomainUser? userEntity = null;
                bool isNewUser = false;

                // --- FIX APPLIED HERE: Robust Two-Step User Fetch ---
                // STEP 1: Use the service layer to perform a safe check. This can hit a cache
                // and its DTO handles type conversion correctly.
                UserDto? userDto = await userService.GetUserByTelegramIdAsync(userId.ToString(), cancellationToken);

                if (userDto != null)
                {
                    // The user exists. Fetch the full entity using the type-safe GetByIdAsync to avoid the SQLite bug.
                    scopedLogger.LogInformation("Existing user DTO found for {UserId}. Fetching full entity by ID: {DbId}", userId, userDto.Id);
                    userEntity = await userRepository.GetByIdAsync(userDto.Id, cancellationToken);
                }

                if (userEntity == null)
                {
                    // This block executes if the user is genuinely new, OR if there was a data inconsistency (DTO existed, but entity didn't).
                    // In either case, the correct action is to register them.
                    scopedLogger.LogInformation("User {UserId} ({SanitizedUsername}) is confirmed new or inconsistent. Proceeding with registration.", userId, sanitizedUsernameForLogs);

                    DomainUser newUserEntity = CreateNewUserEntity(telegramUser);

                    // The RegisterUserAsync should handle the repository AddAsync call.
                    _ = await userService.RegisterUserAsync(
                        new RegisterUserDto { Username = newUserEntity.Username, TelegramId = newUserEntity.TelegramId, Email = newUserEntity.Email },
                        cancellationToken,
                        userEntityToRegister: newUserEntity
                    );

                    userEntity = newUserEntity; // The newly created entity is our user.
                    isNewUser = true;
                    scopedLogger.LogInformation("User {UserId} ({SanitizedUsername}) registered and cached successfully.", userId, _logSanitizer.Sanitize(userEntity.Username));
                }
                else
                {
                    // This block executes only if the user was found successfully in the database.
                    isNewUser = false;
                    scopedLogger.LogInformation("Existing user {UserId} ({SanitizedUsername}) found in database.", userId, _logSanitizer.Sanitize(userEntity.Username));
                }

                // Clear any previous state for existing users.
                if (!isNewUser && _stateMachine != null)
                {
                    await _stateMachine.ClearStateAsync(telegramUser.Id, cancellationToken);
                }

                // Update the welcome message with the final details.
                await EditWelcomeMessageWithDetailsAsync(userId, messageId, userEntity.Username, !isNewUser, messageSender, cancellationToken);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
            {
                scopedLogger.LogWarning(ex, "Registration attempted for already existing user {UserId}. Showing main menu.", userId);
                // Try to fetch the user entity again
                UserDto? existingUserDto = await userService.GetUserByTelegramIdAsync(userId.ToString(), cancellationToken);
                string username = existingUserDto?.Username ?? "User";
                await EditWelcomeMessageWithDetailsAsync(userId, messageId, username, true, messageSender, cancellationToken);
            }
            catch (Exception ex)
            {
                scopedLogger.LogCritical(ex, "A critical, unhandled error occurred during the background registration process for UserID {UserId}.", userId);
                await messageSender.EditMessageTextAsync(userId, messageId, "An internal server error occurred. Our team has been notified. Please try again later.", cancellationToken: CancellationToken.None);
            }
            finally
            {
                _ = await cacheService.ReleaseLockAsync(lockKey, lockToken);
                scopedLogger.LogTrace("Registration lock for UserID {UserId} released.", userId);
            }
        }

        // --- FIX: Use DomainUser alias and Telegram.Bot.Types.User explicitly ---
        private DomainUser CreateNewUserEntity(TGBotTypes.User telegramUser)
        {
            string telegramUserId = telegramUser.Id.ToString();
            string effectiveUsername = GetEffectiveUsername(telegramUser);
            string sanitizedUsername = _logSanitizer.Sanitize(effectiveUsername);

            Guid userGuid = Guid.NewGuid();
            DomainUser newUser = new()
            {
                Id = userGuid,
                Username = sanitizedUsername,
                TelegramId = telegramUserId,
                Email = $"{telegramUserId}@telegram.placeholder.email",
                Level = UserLevel.Free,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                EnableGeneralNotifications = true,
                EnableVipSignalNotifications = false,
                EnableRssNewsNotifications = true,
                PreferredLanguage = SanitizeLanguageCode(telegramUser.LanguageCode),
                TokenWallet = new TokenWallet(Guid.NewGuid(), userGuid, 0.0m, true, DateTime.UtcNow, DateTime.UtcNow)
            };
            return newUser;
        }


        // --- FIX: Explicitly use Telegram.Bot.Types.User ---
        private string GetEffectiveUsername(TGBotTypes.User telegramUser)
        {
            string? name = !string.IsNullOrWhiteSpace(telegramUser.Username)
                ? telegramUser.Username
                : $"{telegramUser.FirstName} {telegramUser.LastName}".Trim();

            return string.IsNullOrWhiteSpace(name) ? $"User_{telegramUser.Id}" : name;
        }

        private string SanitizeLanguageCode(string? langCode)
        {
            return string.IsNullOrWhiteSpace(langCode) ? "en" : Regex.IsMatch(langCode, @"^[a-zA-Z]{2}(-[a-zA-Z]{2})?$") ? langCode : "en";
        }

        // In StartCommandHandler.cs

        [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
        private async Task HandleShowMainMenuCallback(TGBotTypes.CallbackQuery callbackQuery, TGBotTypes.User user, TGBotTypes.Message originalMessage, CancellationToken cancellationToken)
        {
            long chatId = originalMessage.Chat.Id;
            _ = originalMessage.MessageId;

            using IServiceScope scope = _scopeFactory.CreateScope();
            ITelegramMessageSender messageSender = scope.ServiceProvider.GetRequiredService<ITelegramMessageSender>();
            ILogger<StartCommandHandler> scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<StartCommandHandler>>();

            scopedLogger.LogInformation("Handling '{Callback}' for UserID: {UserId}. Strategy: Delete and Send New.",
                ShowMainMenuCallback, user.Id);

            try
            {
                // Answer the callback to remove the "loading" state from the button.
                await messageSender.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);


                // 2. Prepare the content for the new main menu.
                string effectiveUsername = GetEffectiveUsername(user);
                string welcomeText = $"🎉 *Welcome back, {TelegramMessageFormatter.EscapeMarkdownV2(effectiveUsername)}!*";
                string messageBody = GenerateWelcomeMessageBody(welcomeText, isExistingUser: true);
                InlineKeyboardMarkup keyboard = GetMainMenuKeyboard();

                // 3. Send the new, text-based main menu.
                await messageSender.SendTextMessageAsync(
                    chatId,
                    messageBody,
                    ParseMode.MarkdownV2,
                    keyboard,
                    cancellationToken
                );
                // --- END OF TARGETED FIX ---

                if (_stateMachine != null)
                {
                    await _stateMachine.ClearStateAsync(user.Id, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                scopedLogger.LogError(ex, "A critical error occurred while handling '{Callback}' for UserID {UserId}",
                    ShowMainMenuCallback, user.Id);
            }
        }

        private Task<TGBotTypes.Message> SendInitialWelcomeMessageAsync(long chatId, string firstName, CancellationToken cancellationToken)
        {
            string sanitizedFirstName = _logSanitizer.Sanitize(firstName);
            string loadingCaption = $"Hello {TelegramMessageFormatter.EscapeMarkdownV2(sanitizedFirstName)}! 👋\n\n" +
                                 "🌟 *Welcome to the Forex AI Analyzer*\n\n" +
                                 "Initializing your profile, please wait...";
            _ = TGBotTypes.InputFile.FromUri("https://i.postimg.cc/CL8sSt8h/Chat-GPT-Image-Jun-20-2025-01-07-32-AM.png");

            return _botClient.SendMessage(
                 chatId: chatId,
                 text: loadingCaption,
                 parseMode: ParseMode.Markdown,
                 replyMarkup: GetMainMenuKeyboard(),
                 cancellationToken: cancellationToken);
        }

        [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
        private Task EditWelcomeMessageWithDetailsAsync(long chatId, int messageId, string username, bool isExistingUser, ITelegramMessageSender messageSender, CancellationToken cancellationToken)
        {
            string sanitizedUsername = _logSanitizer.Sanitize(username);

            // Determine the final welcome header.
            string welcomeHeader = isExistingUser
                ? $"🎉 *Welcome back, {TelegramMessageFormatter.EscapeMarkdownV2(sanitizedUsername)}!*"
                : $"Hello {TelegramMessageFormatter.EscapeMarkdownV2(sanitizedUsername)}! 👋\n\n🌟 *Welcome to the Forex AI Analyzer*";

            // Generate the full message body, which will become the new caption.
            string finalCaption = GenerateWelcomeMessageBody(welcomeHeader, isExistingUser);

            // --- THIS IS THE FIX ---
            // Use EditMessageCaptionAsync to change the caption of a message that already has media.
            // We call _botClient directly as it's guaranteed to have this method.
            return messageSender.EditMessageTextAsync(
                chatId: chatId,
                messageId: messageId,
                text: finalCaption,
                parseMode: TGBotTypes.Enums.ParseMode.Markdown,
                replyMarkup: GetMainMenuKeyboard(), // Cast to the correct type
                cancellationToken: cancellationToken);
        }
        [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
        private string GenerateWelcomeMessageBody(string welcomeHeader, bool isExistingUser)
        {
            // Retrieve a random piece of advice.
            string advice = _adviceService.GetNextUniqueAdviceForChannel(); // Using the injected service

            // Format the advice with an emoji and spacing.
            string formattedAdvice = $"\n\n💡 {advice}"; // Example emoji: 💡

            return $"{welcomeHeader}\n\nYour trusted companion for trading signals and market analysis." +
                   $"{formattedAdvice}\n\n" + // Inject the advice here
                   "📊 *Available Features:*\n• 📈 Real-time alerts & signals\n• 💎 Professional trading tools\n• 📰 In-depth news analysis\n" +
                   (isExistingUser ? "• 💼 Portfolio tracking\n• 🔔 Customizable notifications\n\n" : "• 💼 Portfolio tracking\n\n") +
                   "Use the menu below or type /help for more information.";
        }


        private InlineKeyboardMarkup GetMainMenuKeyboard()
        {
            return MenuCommandHandler.GetMainMenuMarkup().keyboard;
        }
    }
}