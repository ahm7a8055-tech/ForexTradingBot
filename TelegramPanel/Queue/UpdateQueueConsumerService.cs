// File: TelegramPanel/Queue/UpdateQueueConsumerService.cs
#region Usings
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;
using StackExchange.Redis;
using System; // Ensure System is referenced
using System.Collections.Generic;
using System.Linq; // Still needed for `Any` on lists etc.
using System.Text;
using System.Threading; // For SemaphoreSlim and CancellationToken
using System.Threading.Tasks; // For Task and Task.Run
using Telegram.Bot;
using Telegram.Bot.Types; // For Update type
using TelegramPanel.Application.Interfaces;
#endregion

namespace TelegramPanel.Queue
{
    public class UpdateQueueConsumerService : BackgroundService
    {
        #region Private Readonly Fields
        private readonly ILogger<UpdateQueueConsumerService> _logger;
        private readonly ITelegramUpdateChannel _updateChannel;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AsyncPolicyWrap _redisResiliencePolicy;
        private readonly AsyncRetryPolicy _processingRetryPolicy;
        private readonly List<long> _adminChatIds;

        // ✅ NEW: Semaphore to control the degree of parallelism for processing updates.
        // This prevents overwhelming the system. The value should be configured based on your environment.
        private readonly SemaphoreSlim _concurrencySemaphore;
        private readonly int _maxConcurrentProcessors;

        private readonly TimeSpan _redisBreakDuration = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _redisReadTimeout = TimeSpan.FromMinutes(1);
        private static readonly Random _jitterer = new Random();
        #endregion

        #region Constructor
        public UpdateQueueConsumerService(IConfiguration configuration,
            ILogger<UpdateQueueConsumerService> logger,
            ITelegramUpdateChannel updateChannel,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _updateChannel = updateChannel ?? throw new ArgumentNullException(nameof(updateChannel));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

            // ✅ NEW: Configure max concurrency from appsettings.json or use a safe default.
            _maxConcurrentProcessors = configuration.GetValue<int>("TelegramPanel:Queue:MaxConcurrentProcessors", 10);
            _concurrencySemaphore = new SemaphoreSlim(_maxConcurrentProcessors, _maxConcurrentProcessors);
            _logger.LogInformation("Update processor configured with a maximum of {MaxConcurrency} concurrent tasks.", _maxConcurrentProcessors);


            // ✅ STRENGTHENED: The circuit breaker now also handles Polly's own timeout exception.
            var circuitBreakerPolicy = Policy
                .Handle<RedisException>()
                .Or<RedisConnectionException>()
                .Or<RedisTimeoutException>()
                .Or<KeyNotFoundException>()
                .Or<TimeoutRejectedException>() // <-- Handles timeouts from the Polly policy
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 3,
                    durationOfBreak: _redisBreakDuration,
                    onBreak: (exception, timespan) => _logger.LogCritical(exception, "Redis Circuit Breaker OPENED for {BreakDuration}. Halting all queue consumption.", timespan),
                    onReset: () => _logger.LogInformation("Redis Circuit Breaker RESET. Resuming normal queue consumption."),
                    onHalfOpen: () => _logger.LogWarning("Redis Circuit Breaker is now HALF-OPEN. The next read will test the connection.")
                );

            var timeoutPolicy = Policy.TimeoutAsync(_redisReadTimeout, TimeoutStrategy.Pessimistic);

            _redisResiliencePolicy = Policy.WrapAsync(timeoutPolicy, circuitBreakerPolicy);

            _adminChatIds = configuration.GetSection("TelegramPanel:AdminUserIds").Get<List<long>>() ?? new List<long>();
            if (_adminChatIds.Count == 0)
            {
                _logger.LogWarning("No AdminUserIds found. Admin notifications will be disabled.");
            }
            else
            {
                _logger.LogInformation("Admin notifications configured for {AdminCount} user(s).", _adminChatIds.Count);
            }

            _processingRetryPolicy = Policy
                .Handle<Exception>(ex => ex is not (OperationCanceledException or TaskCanceledException))
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryAttempt, context) =>
                    {
                        var updateId = context.TryGetValue("UpdateId", out var id) ? (int?)id : null;
                        _logger.LogWarning(exception, "PollyRetry: Processing update {UpdateId} failed. Retrying in {TimeSpan} (attempt {RetryAttempt}).",
                            updateId, timeSpan, retryAttempt);
                    });
        }
        #endregion

        #region Main Execution Logic

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Update Queue Consumer Service is starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // The _redisResiliencePolicy wraps the entire dequeuing and processing initiation.
                    // If the circuit breaker is open, this ExecuteAsync will immediately throw BrokenCircuitException.
                    await _redisResiliencePolicy.ExecuteAsync(async (ct) =>
                    {
                        _logger.LogTrace("Polling Redis queue for updates...");

                        // Use await foreach to process items as they become available from the channel.
                        // The semaphore inside ProcessSingleUpdateConcurrentlyAsync will limit how many
                        // actual processing tasks run in parallel.
                        await foreach (var update in _updateChannel.ReadAllAsync(ct).WithCancellation(ct))
                        {
                            if (update == null) continue; // Should not happen if ReadAllAsync is implemented correctly

                            _logger.LogTrace("Dequeued update {UpdateId} of type {UpdateType}.", update.Id, update.Type);

                            // ✅ MODIFIED: Fire-and-forget the processing task using Task.Run.
                            // This allows the await foreach loop to continue fetching the next message
                            // while the processing happens in the background, controlled by the semaphore.
                            _ = Task.Run(() => ProcessSingleUpdateConcurrentlyAsync(update, ct), ct);
                        }
                        // If ReadAllAsync finishes or throws and is caught below,
                        // this await foreach loop will exit and the outer while loop will continue.

                    }, stoppingToken).ConfigureAwait(false); // Pass stoppingToken to ExecuteAsync
                }
                catch (BrokenCircuitException)
                {
                    _logger.LogWarning("Redis circuit is OPEN. Pausing consumption for {BreakDuration}.", _redisBreakDuration);
                    // Use the jittered delay to avoid thundering herd.
                    await Task.Delay(GetJitteredDelay(_redisBreakDuration), stoppingToken).ConfigureAwait(false);
                }
                catch (TimeoutRejectedException)
                {
                    // A timeout on a long-poll (if _updateChannel used one) is normal. It means no messages arrived.
                    // Log it at a low level and immediately loop again to start the next poll.
                    _logger.LogTrace("Redis queue read timed out (no new messages). Polling again immediately.");
                    // No need for delay here, the loop will naturally restart.
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Update Queue Consumer Service was cancelled gracefully.");
                    break; // Exit the while loop if cancellation occurred.
                }
                catch (Exception ex)
                {
                    // A truly unexpected error that wasn't handled by resilience policies or specific catches.
                    _logger.LogCritical(ex, "An unhandled exception ({ExceptionType}) occurred in the main consumer loop. Pausing for {Delay} before retrying.", ex.GetType().Name, TimeSpan.FromSeconds(10));
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
                }
            }
        }

        // This method is now correctly fire-and-forgotten by Task.Run in ExecuteAsync.
        private async Task ProcessSingleUpdateConcurrentlyAsync(Telegram.Bot.Types.Update update, CancellationToken cancellationToken)
        {
            // Wait until a "slot" is available in the semaphore.
            // This safely limits concurrency.
            // Pass the cancellation token to WaitAsync.
            await _concurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // Once a slot is acquired, execute the processing logic.
                // This call is awaited here to ensure the semaphore is released correctly in the finally block.
                await ProcessSingleUpdateWithRetries(update, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // This catch block is for exceptions that might occur *after* acquiring the semaphore
                // but *before* ProcessSingleUpdateWithRetries itself catches and handles them permanently.
                // For instance, an exception within the _concurrencySemaphore.WaitAsync or the try block
                // itself that isn't caught by the inner policies.
                _logger.LogError(ex, "Error during concurrent processing of update {UpdateId}. This might be a transient issue.", update.Id);
                // Depending on your needs, you might want to re-evaluate the semaphore acquisition if a critical error happens here.
            }
            finally
            {
                // ✅ CRUCIAL: Release the semaphore slot, allowing another task to start.
                // This is in a 'finally' block to guarantee it runs even if ProcessSingleUpdateWithRetries throws an unhandled exception.
                _concurrencySemaphore.Release();
            }
        }
        #endregion

        #region Private Helper for Processing
        private async Task ProcessSingleUpdateWithRetries(Telegram.Bot.Types.Update update, CancellationToken stoppingToken)
        {
            var pollyContext = new Polly.Context($"UpdateProcessing_{update.Id}", new Dictionary<string, object>
            {
                { "UpdateId", update.Id },
                { "UpdateType", update.Type.ToString() }
            });

            using (_logger.BeginScope(pollyContext.ToDictionary(kv => kv.Key, kv => (object?)kv.Value)))
            {
                try
                {
                    // Pass the cancellation token to ExecuteAsync so it can be respected by the policy.
                    await _processingRetryPolicy.ExecuteAsync(async (context, ct) =>
                    {
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var updateProcessor = scope.ServiceProvider.GetRequiredService<ITelegramUpdateProcessor>();
                        await updateProcessor.ProcessUpdateAsync(update, ct).ConfigureAwait(false); // Pass ct to the actual processor
                    }, pollyContext, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // This catch block is for exceptions that _processingRetryPolicy did NOT handle and rethrow.
                    // This means the retries have been exhausted, or the exception type was not configured for retry.
                    var errorMessage = new StringBuilder();
                    errorMessage.AppendLine("❌ *PERMANENT FAILURE: Update Processing Failed*");
                    errorMessage.AppendLine($"*Update ID:* `{update.Id}`");
                    errorMessage.AppendLine($"*Update Type:* `{update.Type}`");
                    errorMessage.AppendLine($"*Message:* `{ex.Message}`");

                    _logger.LogError(ex, "Update {UpdateId} failed processing permanently after all retries. NOTIFYING ADMIN.", update.Id);
                    // IMPORTANT: Do not await NotifyAdminAsync here if you want the main loop to continue quickly.
                    // Use _ = NotifyAdminAsync(...) to fire-and-forget the admin notification.
                    _ = NotifyAdminAsync(errorMessage.ToString());
                }
            }
        }
        #endregion

        #region Admin Notification Helper
        private async Task NotifyAdminAsync(string message)
        {
            if (_adminChatIds == null || _adminChatIds.Count == 0) return;

            _logger.LogInformation("Sending critical notification to {AdminCount} admin(s).", _adminChatIds.Count);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

            foreach (var adminId in _adminChatIds)
            {
                try
                {
                    // Use CancellationToken.None for admin notifications as we don't want these to be cancelled easily.
                    await botClient.SendMessage(
                        chatId: adminId,
                        text: message,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                        cancellationToken: CancellationToken.None // Use None or a long-lived token for critical notifications.
                    ).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send notification to admin (ChatID: {AdminChatId}).", adminId);
                }
            }
        }
        #endregion

        #region Resilience Helpers
        private static TimeSpan GetJitteredDelay(TimeSpan baseDelay)
        {
            if (baseDelay == TimeSpan.Zero) return TimeSpan.Zero;

            var jitterRange = baseDelay.TotalMilliseconds * 0.2;
            var actualJitterRange = Math.Max(0, jitterRange);
            var randomJitter = _jitterer.NextDouble() * (actualJitterRange * 2) - actualJitterRange;

            return baseDelay + TimeSpan.FromMilliseconds(randomJitter);
        }
        #endregion

        #region Service Lifecycle
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Update Queue Consumer Service stop requested.");

            // Signal cancellation to any tasks waiting on the semaphore or processing.
            // This is crucial for graceful shutdown. The tokens passed to Task.Run and WaitAsync
            // will pick up this cancellation.

            await base.StopAsync(cancellationToken).ConfigureAwait(false); // Crucial to call base method
            _logger.LogInformation("Update Queue Consumer Service stop complete.");
        }
        #endregion
    }
}