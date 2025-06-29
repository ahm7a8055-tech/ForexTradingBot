#region Usings
// --- Aliases for Type Safety ---
// --- Project and System ---
using Application.Common.Interfaces;
using Application.DTOs.Settings; // --- ADDED: For ForceJoinSettingsDto ---
using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot; // --- ADDED: For ITelegramBotClient ---
using Telegram.Bot.Exceptions; // --- ADDED: For API exception handling ---
using Telegram.Bot.Types.Enums; // --- ADDED: For ChatMemberStatus ---
using Telegram.Bot.Types.ReplyMarkups;
using TelegramPanel.Application.Interfaces;
using static TelegramPanel.Infrastructure.ActualTelegramMessageActions;
using TGBotTypes = Telegram.Bot.Types;
#endregion

namespace TelegramPanel.Application.Pipeline
{
    /// <summary>
    /// A security-hardened, performant, and resilient middleware for authenticating and authorizing Telegram updates.
    /// It performs a fast, cached check for user existence before fetching the full domain entity,
    /// populates the user context, and enforces channel membership rules.
    /// </summary>
    public class AuthenticationMiddleware : ITelegramMiddleware
    {
        private readonly ILogger<AuthenticationMiddleware> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggingSanitizer _logSanitizer;
        private readonly ITelegramBotClient _botClient; // --- ADDED ---

        public AuthenticationMiddleware(
            ILogger<AuthenticationMiddleware> logger,
            IServiceProvider serviceProvider,
            ILoggingSanitizer logSanitizer,
            ITelegramBotClient botClient) // --- ADDED ---
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logSanitizer = logSanitizer ?? throw new ArgumentNullException(nameof(logSanitizer));
            _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient)); // --- ADDED ---
        }

        /// <summary>
        /// Asynchronously processes an incoming Telegram update, handling user authentication and authorization before passing it to the next middleware.
        /// </summary>
        public async Task InvokeAsync(TGBotTypes.Update update, TelegramPipelineDelegate next, CancellationToken cancellationToken)
        {
            #region Update Validation
            var telegramUser = update.Message?.From ?? update.CallbackQuery?.From;

            if (telegramUser is null)
            {
                _logger.LogWarning("Update {UpdateId} received without a valid 'From' user. Halting pipeline.", update.Id);
                return;
            }
            #endregion

            using (_logger.BeginScope("AuthMiddleware: UpdateId={UpdateId}, UserId={UserId}", update.Id, telegramUser.Id))
            {
                // REFACTORED: ALL commands now go through the full authentication and authorization flow.
                // The special handling for /start is now done inside AuthenticateAndAuthorizeAsync.
                await AuthenticateAndAuthorizeAsync(update, telegramUser, next, cancellationToken);
            }
        }

        /// <summary>
        /// Handles the core authentication and authorization logic: checks for user existence, verifies channel membership, and sets the user context.
        /// </summary>
        private async Task AuthenticateAndAuthorizeAsync(TGBotTypes.Update update, TGBotTypes.User telegramUser, TelegramPipelineDelegate next, CancellationToken cancellationToken)
        {
            #region Dependency Resolution
            await using var scope = _serviceProvider.CreateAsyncScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var userContext = scope.ServiceProvider.GetRequiredService<IUserContext>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            #endregion

            try
            {
                // --- STEP 1: AUTHENTICATION ---
                #region Authentication Flow
                var userDto = await userService.GetUserByTelegramIdAsync(telegramUser.Id.ToString(), cancellationToken);

                // For the /start command, a user might not exist yet.
                // This is a special case: we let them proceed to the StartCommandHandler
                // which is responsible for creating the user.
                bool isStartCommand = update.Message?.Text?.StartsWith("/start") == true;
                if (userDto is null)
                {
                    if (isStartCommand)
                    {
                        _logger.LogInformation("New user detected with /start command. Passing to StartCommandHandler for registration.");
                        await next(update, cancellationToken);
                        return;
                    }

                    // If it's any other command and the user doesn't exist, deny access.
                    await HandleUnauthenticatedAccessAsync(telegramUser.Id, scope, cancellationToken);
                    return;
                }

                var userEntity = await userRepository.GetByIdAsync(userDto.Id, cancellationToken);
                if (userEntity is null)
                {
                    // This is a data inconsistency (cache has user, DB doesn't).
                    _logger.LogCritical("Data Inconsistency: User DTO found for UserID {UserId}, but the full domain entity was not. Invalidating cache.", telegramUser.Id);
                    var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
                    await cacheService.RemoveAsync($"user:telegram_id:{telegramUser.Id}");
                    await HandleUnauthenticatedAccessAsync(telegramUser.Id, scope, cancellationToken);
                    return;
                }
                userContext.SetCurrentUser(userEntity);
                _logger.LogInformation("User ({SanitizedUsername}) authenticated successfully.", _logSanitizer.Sanitize(userEntity.Username));
                #endregion

                // --- STEP 2: AUTHORIZATION (FORCE JOIN CHECK) ---
                #region Force Join Channel Check
                var forceJoinSettings = await settingsService.GetForceJoinSettingsAsync(cancellationToken);

                if (forceJoinSettings is { IsEnabled: true } && forceJoinSettings.ChannelId != 0)
                {
                    try
                    {
                        var chatMember = await _botClient.GetChatMember(
                            chatId: forceJoinSettings.ChannelId, // --- USE ID ---
                            userId: telegramUser.Id,
                            cancellationToken: cancellationToken);

                        if (chatMember.Status is ChatMemberStatus.Left or ChatMemberStatus.Kicked)
                        {
                            await HandleNotAMemberAsync(telegramUser.Id, forceJoinSettings, scope, cancellationToken);
                            return; // Halt the pipeline
                        }
                        _logger.LogTrace("User {UserId} confirmed as member of channel {ChannelId}.", telegramUser.Id, forceJoinSettings.ChannelId);
                    }
                    catch (ApiRequestException apiEx) when (apiEx.Message.Contains("user not found"))
                    {
                        _logger.LogWarning("User {UserId} not found in channel {ChannelId}. Treating as not a member.", telegramUser.Id, forceJoinSettings.ChannelId);
                        await HandleNotAMemberAsync(telegramUser.Id, forceJoinSettings, scope, cancellationToken);
                        return; // Halt the pipeline
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCritical(ex, "Failed to check channel membership for User {UserId} in {ChannelId}. This is likely a configuration error.", telegramUser.Id, forceJoinSettings.ChannelId);
                        await HandleCriticalFailureAsync(telegramUser.Id, scope, "A server error occurred while verifying your access.", cancellationToken);
                        return; // Halt the pipeline
                    }
                }
                #endregion

                // --- STEP 3: PIPELINE CONTINUATION ---
                // If we've reached this point, the user is authenticated AND has passed all authorization checks.
                _logger.LogInformation("User authorized. Proceeding to next handler for action.");
                await next(update, cancellationToken);
            }
            catch (Exception ex)
            {
                #region Critical Failure Handling
                _logger.LogCritical(ex, "A critical, unhandled error occurred during the authentication/authorization process.");
                await HandleCriticalFailureAsync(telegramUser.Id, scope, "A server error occurred during authentication.", cancellationToken);
                #endregion
            }
        }

        #region Private Handlers
        /// <summary>
        /// Sends a message to a user who is not a member of the required channel. --- NEW ---
        /// </summary>
        private async ValueTask HandleNotAMemberAsync(long telegramId, ForceJoinSettingsDto settings, AsyncServiceScope scope, CancellationToken cancellationToken)
        {
            _logger.LogWarning("Access denied for UserID {UserId} due to not being a member of required channel {ChannelId}.", telegramId, settings.ChannelId);
            var messageSender = scope.ServiceProvider.GetRequiredService<ITelegramMessageSender>();

            // --- 1. DEFINE THE MESSAGE TEXT ---
            // Use the custom message from settings, or a default one if it's not set.
            var messageText = !string.IsNullOrWhiteSpace(settings.Message)
                ? settings.Message
                : "⛔️ **Access Restricted**\n\nTo use this bot, you must first join our official channel.";

            // --- 2. CREATE THE INLINE BUTTON ---
            // The button's URL should be a proper t.me link.
            // If the admin provided a username like @mychannel, we convert it.
            // If they provided a full link, we use it directly.
            string channelUrl;
            if (settings.ChannelLink.StartsWith("@"))
            {
                channelUrl = $"https://t.me/{settings.ChannelLink.Substring(1)}";
            }
            else if (settings.ChannelLink.StartsWith("https://"))
            {
                channelUrl = settings.ChannelLink;
            }
            else
            {
                // Fallback for private channels where we only have the ID.
                // This might not always work if the channel doesn't have a public link,
                // but it's the best we can do.
                // A better approach for private channels is to create an invite link.
                // For now, we just show the stored link.
                channelUrl = settings.ChannelLink;
                // Log a warning if the link isn't ideal
                _logger.LogWarning("The configured ChannelLink '{ChannelLink}' is not a standard URL or username. The join button may not work for all users.", channelUrl);
            }

            var joinButton = InlineKeyboardButton.WithUrl("✅ Join Channel", channelUrl);
            var keyboard = new InlineKeyboardMarkup(joinButton);

            // --- 3. SEND THE MESSAGE WITH THE BUTTON ---
            try
            {
                await messageSender.SendTextMessageAsync(
                    chatId: telegramId,
                    text: messageText,
                    parseMode: ParseMode.Markdown,
                    replyMarkup: keyboard, // Attach the keyboard with the button
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send 'Join Channel' message with button to UserID {UserId}.", telegramId);
            }
        }
        private async ValueTask HandleUnauthenticatedAccessAsync(long telegramId, AsyncServiceScope scope, CancellationToken cancellationToken)
        {
            _logger.LogWarning("Unauthenticated access attempt by UserID {UserId}. Access denied.", telegramId);
            var messageSender = scope.ServiceProvider.GetRequiredService<ITelegramMessageSender>();

            try
            {
                await messageSender.SendTextMessageAsync(
                    chatId: telegramId,
                    text: "⛔️ **Access Denied**\n\nYou are not authorized to use this command. Please use /start to register.",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send 'Access Denied' message to UserID {UserId}.", telegramId);
            }
        }

        private async ValueTask HandleCriticalFailureAsync(long telegramId, AsyncServiceScope scope, string message, CancellationToken cancellationToken)
        {
            var messageSender = scope.ServiceProvider.GetRequiredService<ITelegramMessageSender>();
            try
            {
                await messageSender.SendTextMessageAsync(
                    chatId: telegramId,
                    text: $"🤖 {message} Our team has been notified. Please try again later.",
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send critical failure message to UserID {UserId}.", telegramId);
            }
        }
        #endregion
    }
}