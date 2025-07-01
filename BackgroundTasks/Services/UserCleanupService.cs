// File: BackgroundTasks/Services/UserCleanupService.cs

#region Usings
using Application.DTOs;
using Application.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
#endregion

namespace BackgroundTasks.Services
{
    /// <summary>
    /// A robust background service to periodically check user reachability and delete those who are unreachable.
    /// It's designed to handle a large number of users by processing them in concurrent batches,
    /// respecting API rate limits, and handling errors gracefully.
    /// </summary>
    public class UserCleanupService
    {
        // --- Injected Dependencies ---
        private readonly ITelegramBotClient _botClient;
        private readonly IUserService _userService;
        private readonly ILogger<UserCleanupService> _logger;

        // --- Configuration Constants for Scalability ---
        private const int MAX_CONCURRENT_API_CALLS = 16;

        /// <summary>
        /// Represents the outcome of checking a single user's reachability.
        /// </summary>
        private enum UserReachabilityStatus
        {
            Reachable,
            UnreachableAndDeleted,
            Skipped, // For invalid data or other non-error skips
            Error    // For actual exceptions during processing
        }

        public UserCleanupService(
            ITelegramBotClient botClient,
            IUserService userService,
            ILogger<UserCleanupService> logger)
        {
            _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Periodically checks all users to identify and delete those who are unreachable (e.g., blocked or deleted their account).
        /// </summary>
        [AutomaticRetry(Attempts = 0)]
        [Queue("default")]
        [JobDisplayName("User Cleanup: Delete Blocked/Deleted Users")] // Kept original name
        public async Task CheckAndDeleteUnreachableUsersAsync() // Kept original name
        {
            using var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;

            _logger.LogInformation("Starting user cleanup job to find and delete unreachable users...");

            List<UserDto>? allUsers;
            try
            {
                // **CRITICAL NOTE ON SCALABILITY**: This remains the primary bottleneck for true "million user" scale.
                allUsers = await _userService.GetAllUsersAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve the user list from the database. Aborting job.");
                return;
            }

            if (allUsers == null || !allUsers.Any())
            {
                _logger.LogInformation("No users found to check. Cleanup job finished.");
                return;
            }

            using var semaphore = new SemaphoreSlim(MAX_CONCURRENT_API_CALLS);

            _logger.LogInformation("Processing {TotalUsersCount} users with a concurrency limit of {MaxConcurrentCalls}.",
                                   allUsers.Count, MAX_CONCURRENT_API_CALLS);

            var processingTasks = allUsers.Select(user =>
                ProcessSingleUserAsync(user, cancellationToken, semaphore)
            ).ToList();

            var results = await Task.WhenAll(processingTasks);

            // Aggregate and log final results for observability.
            int reachableCount = results.Count(r => r == UserReachabilityStatus.Reachable);
            int deletedCount = results.Count(r => r == UserReachabilityStatus.UnreachableAndDeleted);
            int skippedOrErrorCount = results.Count(r => r == UserReachabilityStatus.Skipped || r == UserReachabilityStatus.Error);

            _logger.LogInformation(
                "User cleanup job finished. Results -> Reachable: {ReachableCount}, Deleted: {DeletedCount}, Skipped/Errors: {SkippedOrErrorCount}.",
                reachableCount, deletedCount, skippedOrErrorCount);
        }

        /// <summary>
        // Processes a single user: checks reachability via Telegram API and triggers deletion if unreachable.
        /// </summary>
        private async Task<UserReachabilityStatus> ProcessSingleUserAsync(UserDto user, CancellationToken cancellationToken, SemaphoreSlim semaphore)
        {
            // --- Step 1: Robust Data Validation ---
            if (user == null || user.Id == Guid.Empty || string.IsNullOrWhiteSpace(user.TelegramId))
            {
                _logger.LogWarning("Skipping user with invalid data (null, empty Guid, or empty TelegramId). User ID: {UserId}", user?.Id);
                return UserReachabilityStatus.Skipped;
            }
            if (!long.TryParse(user.TelegramId, out var telegramUserId))
            {
                _logger.LogWarning("Skipping user with invalid TelegramId format. User ID: {UserId}, TelegramId: {TelegramId}", user.Id, user.TelegramId);
                return UserReachabilityStatus.Skipped;
            }
            // --- End Validation ---

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // --- Step 2: The "Stealth" Reachability Check ---
                // Using SendChatActionAsync is preferred for modern async code with cancellation support.
                await _botClient.SendChatAction(telegramUserId, ChatAction.Typing);

                _logger.LogDebug("User {UserId} ({TelegramId}) is reachable.", user.Id, telegramUserId);
                return UserReachabilityStatus.Reachable;
            }
            catch (ApiRequestException ex)
            {
                // --- Step 3: Handle Specific API Errors Indicating Unreachability ---
                bool isBlockedOrForbidden = ex.ErrorCode == 403 || ex.Message.Contains("bot was blocked by the user", StringComparison.OrdinalIgnoreCase);
                bool isNotFound = ex.ErrorCode == 400 && ex.Message.Contains("user not found", StringComparison.OrdinalIgnoreCase);

                if (isBlockedOrForbidden || isNotFound)
                {
                    string reason = isBlockedOrForbidden ? "BlockedOrForbidden" : "DeletedOrNotFound";
                    _logger.LogInformation("User {UserId} ({TelegramId}) is unreachable. Reason: {Reason}. Triggering deletion.", user.Id, telegramUserId, reason);

                    // **THE FIX IS HERE**: Directly call the deletion helper.
                    return await TriggerDeleteUserAsync(user.Id, cancellationToken);
                }
                else
                {
                    // This is a different, unexpected API error.
                    _logger.LogWarning(ex, "An unexpected Telegram API error occurred for user {UserId} ({TelegramId}). ErrorCode: {ErrorCode}, Message: {ErrorMessage}.",
                                       user.Id, telegramUserId, ex.ErrorCode, ex.Message);
                    return UserReachabilityStatus.Error;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("User check for {UserId} ({TelegramId}) was cancelled.", user.Id, telegramUserId);
                return UserReachabilityStatus.Skipped;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A critical, non-API error occurred while checking user {UserId} ({TelegramId}).", user.Id, telegramUserId);
                return UserReachabilityStatus.Error;
            }
            finally
            {
                // --- Step 4: Always Release the Semaphore ---
                semaphore.Release();
            }
        }

        /// <summary>
        /// A private helper to safely call the `IUserService.DeleteUserAsync` method.
        /// This encapsulates the database operation and its specific error handling.
        /// </summary>
        private async Task<UserReachabilityStatus> TriggerDeleteUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                // Call the method that exists on your IUserService.
                await _userService.DeleteUserAsync(userId, cancellationToken);
                return UserReachabilityStatus.UnreachableAndDeleted; // Success
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute DeleteUserAsync for UserId {UserId} in the database.", userId);
                return UserReachabilityStatus.Error; // The DB operation failed.
            }
        }
    }
}