#region Usings
// --- Aliases for Type Safety ---
using DomainUser = Domain.Entities.User;
using TGBotTypes = Telegram.Bot.Types;

// --- Project and System ---
using Application.Common.Interfaces;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;
using TelegramPanel.Application.Interfaces;
using TelegramPanel.Infrastructure;
using static TelegramPanel.Infrastructure.ActualTelegramMessageActions;
#endregion

namespace TelegramPanel.Application.Pipeline
{
    /// <summary>
    /// A security-hardened, performant, and resilient middleware for authenticating Telegram updates.
    /// It performs a fast, cached check for user existence before fetching the full domain entity
    /// to populate the user context, ensuring both speed and correctness. This middleware is specifically
    /// designed to handle data type inconsistencies, such as GUIDs stored as TEXT in SQLite, and to
    /// self-heal from stale cache entries.
    /// </summary>
    public class AuthenticationMiddleware : ITelegramMiddleware
    {
        private readonly ILogger<AuthenticationMiddleware> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggingSanitizer _logSanitizer;

        public AuthenticationMiddleware(
            ILogger<AuthenticationMiddleware> logger,
            IServiceProvider serviceProvider,
            ILoggingSanitizer logSanitizer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logSanitizer = logSanitizer ?? throw new ArgumentNullException(nameof(logSanitizer));
        }

        /// <summary>
        /// Asynchronously processes an incoming Telegram update, handling user authentication before passing it to the next middleware.
        /// </summary>
        /// <param name="update">The incoming Telegram update to be processed.</param>
        /// <param name="next">The delegate representing the next middleware in the pipeline.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task InvokeAsync(TGBotTypes.Update update, TelegramPipelineDelegate next, CancellationToken cancellationToken)
        {
            #region Update Validation
            // Extract the user who sent the update. If no user can be identified, we cannot proceed.
            var telegramUser = update.Message?.From ?? update.CallbackQuery?.From;

            if (telegramUser is null)
            {
                _logger.LogWarning("Update {UpdateId} received without a valid 'From' user. Halting pipeline.", update.Id);
                return; // Halt the pipeline as there is no user to authenticate.
            }
            #endregion

            // Use a logger scope to automatically tag all logs within this block with relevant context.
            using (_logger.BeginScope("AuthMiddleware: UpdateId={UpdateId}, UserId={UserId}", update.Id, telegramUser.Id))
            {
                #region Public Command Handling (/start)
                // The /start command is public and should bypass the authentication check to allow new user registration.
                if (update.Message?.Text?.StartsWith("/start") == true)
                {
                    _logger.LogInformation("Passing through public /start command to next handler.");
                    await next(update, cancellationToken);
                    return;
                }
                #endregion

                #region Authentication Flow
                // For all other commands, proceed with the full authentication and context-setting flow.
                await AuthenticateAndProceedAsync(update, telegramUser, next, cancellationToken);
                #endregion
            }
        }

        /// <summary>
        /// Handles the core authentication logic: checks for user existence, fetches the full entity, and sets the user context.
        /// </summary>
        /// <param name="update">The original Telegram update.</param>
        /// <param name="telegramUser">The Telegram user object extracted from the update.</param>
        /// <param name="next">The delegate for the next middleware in the pipeline.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task AuthenticateAndProceedAsync(TGBotTypes.Update update, TGBotTypes.User telegramUser, TelegramPipelineDelegate next, CancellationToken cancellationToken)
        {
            #region Dependency Resolution
            // Create a new dependency injection scope for this request to ensure services are isolated.
            await using var scope = _serviceProvider.CreateAsyncScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var userContext = scope.ServiceProvider.GetRequiredService<IUserContext>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            #endregion

            try
            {
                #region Fast DTO Check
                // --- STEP 1: FAST, CACHED DTO CHECK ---
                // Quickly verify if the user exists at all using a service that likely leverages caching.
                var userDto = await userService.GetUserByTelegramIdAsync(telegramUser.Id.ToString(), cancellationToken);

                if (userDto is null)
                {
                    // If the fast check fails, the user is unauthenticated.
                    await HandleUnauthenticatedAccessAsync(telegramUser.Id, scope, cancellationToken);
                    return;
                }
                #endregion

                #region Full Entity Fetch & SQLite BUG FIX
                // --- STEP 2: FETCH THE FULL DOMAIN ENTITY ---
                // The previous fix for the SQLite Guid/string cast error is applied here by using the type-safe ID from the DTO.
                var userEntity = await userRepository.GetByIdAsync(userDto.Id, cancellationToken);
                #endregion

                #region Data Consistency Handling
                if (userEntity is null)
                {
                    // --- SELF-HEALING & USER EXPERIENCE FIX ---
                    // This block executes if the cache returned a user (stale data), but the database has no such user.
                    // This is a data inconsistency that we must log and correct.
                    _logger.LogCritical("Data Inconsistency: User DTO found for UserID {UserId}, but the full domain entity was not. This indicates a stale cache entry.", telegramUser.Id);

                    // Self-Heal: Attempt to remove the invalid entry from the cache to prevent this from reoccurring.
                    var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
                    await cacheService.RemoveAsync($"user:telegram_id:{telegramUser.Id}");
                    _logger.LogInformation("Stale cache entry for UserID {UserId} has been invalidated.", telegramUser.Id);

                    // User Experience: From the user's perspective, they are not authorized because they don't truly exist.
                    // Treat this as a standard unauthenticated access attempt.
                    await HandleUnauthenticatedAccessAsync(telegramUser.Id, scope, cancellationToken);
                    return;
                }
                #endregion

                #region Context Setting and Pipeline Continuation
                // --- STEP 3: SET THE CONTEXT WITH THE CORRECT TYPE ---
                userContext.SetCurrentUser(userEntity);

                var sanitizedUsername = _logSanitizer.Sanitize(userEntity.Username);
                _logger.LogInformation("User ({SanitizedUsername}) authenticated successfully. Proceeding to next handler.", sanitizedUsername);

                await next(update, cancellationToken);
                #endregion
            }
            catch (Exception ex)
            {
                #region Critical Failure Handling
                _logger.LogCritical(ex, "A critical, unhandled error occurred during the authentication process.");
                await HandleCriticalFailureAsync(telegramUser.Id, scope, "A server error occurred during authentication.", cancellationToken);
                #endregion
            }
        }

        /// <summary>
        /// Sends a standardized "Access Denied" message to a user who failed authentication.
        /// </summary>
        /// <param name="telegramId">The chat ID of the user to send the message to.</param>
        /// <param name="scope">The dependency injection scope to resolve the message sending service.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        private async ValueTask HandleUnauthenticatedAccessAsync(long telegramId, AsyncServiceScope scope, CancellationToken cancellationToken)
        {
            #region Message Sending
            _logger.LogWarning("Unauthenticated access attempt by UserID {UserId}. Access denied.", telegramId);
            var messageSender = scope.ServiceProvider.GetRequiredService<ITelegramMessageSender>();

            try
            {
                await messageSender.SendTextMessageAsync(
                    chatId: telegramId,
                    text: "⛔️ **Access Denied**\n\nYou are not authorized to use this command. Please use /start to register.",
                    parseMode: TGBotTypes.Enums.ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send 'Access Denied' message to UserID {UserId}.", telegramId);
            }
            #endregion
        }

        /// <summary>
        /// Sends a standardized message to a user when a critical, unrecoverable error occurs during their request.
        /// </summary>
        /// <param name="telegramId">The chat ID of the user to send the message to.</param>
        /// <param name="scope">The dependency injection scope to resolve the message sending service.</param>
        /// <param name="message">The user-facing part of the error message to display.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        private async ValueTask HandleCriticalFailureAsync(long telegramId, AsyncServiceScope scope, string message, CancellationToken cancellationToken)
        {
            #region Message Sending
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
            #endregion
        }
    }
}