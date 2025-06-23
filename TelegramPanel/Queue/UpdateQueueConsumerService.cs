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
using System;
using System.Collections.Concurrent; // For BlockingCollection
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading; // For SemaphoreSlim and CancellationToken
using System.Threading.Tasks;
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
        private readonly ITelegramUpdateChannel _updateChannel; // Source of incoming updates from primary Redis queue
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AsyncPolicyWrap _redisResiliencePolicy;
        private readonly AsyncRetryPolicy _processingRetryPolicy;

        // --- Concurrency and Throughput Control ---
        // BlockingCollection acts as our explicit in-memory queue for tasks awaiting processing.
        private readonly BlockingCollection<Telegram.Bot.Types.Update> _pendingProcessingQueue;
        private readonly int _maxConcurrentProcessors; // Max number of concurrent processing tasks

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

            // Configure max concurrent processors. This will be the capacity of our blocking collection.
            // For 10000+ requests, set this to a high number, but be mindful of system resources.
            // The BlockingCollection will buffer items up to this capacity.
            _maxConcurrentProcessors = configuration.GetValue<int>("TelegramPanel:Queue:MaxConcurrentProcessors", 10000);
            // Use BlockingCollection as an explicit, bounded, thread-safe queue.
            _pendingProcessingQueue = new BlockingCollection<Telegram.Bot.Types.Update>(
                new ConcurrentQueue<Telegram.Bot.Types.Update>(), // Use ConcurrentQueue as the underlying collection
                _maxConcurrentProcessors // The bounded capacity. If full, .Add() will block.
            );
            _logger.LogInformation("Update processor configured with a maximum of {MaxConcurrency} concurrent processing tasks. Using a bounded collection as an explicit queue.", _maxConcurrentProcessors);

            // Resilience policies for Redis interaction.
            var circuitBreakerPolicy = Policy
                .Handle<RedisException>()
                .Or<RedisConnectionException>()
                .Or<RedisTimeoutException>()
                .Or<KeyNotFoundException>()
                .Or<TimeoutRejectedException>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5, // Slightly more resilient before breaking
                    durationOfBreak: TimeSpan.FromSeconds(30), // Shorter break duration
                    onBreak: (exception, timespan) => _logger.LogCritical(exception, "Redis Circuit Breaker OPENED for {BreakDuration}. Halting all queue consumption.", timespan),
                    onReset: () => _logger.LogInformation("Redis Circuit Breaker RESET. Resuming normal queue consumption."),
                    onHalfOpen: () => _logger.LogWarning("Redis Circuit Breaker is now HALF-OPEN. The next read will test the connection.")
                );

            var timeoutPolicy = Policy.TimeoutAsync(_redisReadTimeout, TimeoutStrategy.Pessimistic);
            _redisResiliencePolicy = Policy.WrapAsync(timeoutPolicy, circuitBreakerPolicy);

            // Retry policy for the actual processing of an update.
            _processingRetryPolicy = Policy
                .Handle<Exception>(ex => ex is not (OperationCanceledException or TaskCanceledException))
                .WaitAndRetryAsync(
                    retryCount: 5, // Increased retries
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
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

            // Task to continuously pull updates from the source channel and add them to the processing queue.
            // This is our producer.
            var producerTask = Task.Run(() => ProduceUpdatesAsync(stoppingToken), stoppingToken);

            // Create and start consumer tasks that will pull updates from the _pendingProcessingQueue.
            // The number of consumer tasks determines the maximum concurrency.
            var consumerTasks = new List<Task>();
            for (int i = 0; i < _maxConcurrentProcessors; i++)
            {
                // Pass the main stoppingToken to each consumer task.
                consumerTasks.Add(Task.Run(() => ConsumeUpdatesAsync(stoppingToken), stoppingToken));
            }

            _logger.LogInformation("Started producer task and {ConsumerCount} consumer tasks.", _maxConcurrentProcessors);

            // Wait for all producer and consumer tasks to complete.
            // This ensures the service doesn't exit prematurely.
            await Task.WhenAll(producerTask, Task.WhenAll(consumerTasks)).ConfigureAwait(false);

            _logger.LogInformation("Update Queue Consumer Service has stopped.");
        }

        /// <summary>
        /// Producer task: Continuously dequeues updates from the source channel (e.g., primary Redis queue)
        /// and adds them to the _pendingProcessingQueue.
        /// </summary>
        private async Task ProduceUpdatesAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Producer task started. Pulling from ITelegramUpdateChannel.");
            try
            {
                // Apply resilience policies to the source fetching operation.
                await _redisResiliencePolicy.ExecuteAsync(async (ct) =>
                {
                    _logger.LogTrace("Polling source channel for updates...");

                    // Use await foreach to get updates from the channel.
                    // This will block if the channel is empty, and will break on cancellation.
                    await foreach (var update in _updateChannel.ReadAllAsync(ct).WithCancellation(ct))
                    {
                        if (update == null) continue; // Safety check

                        _logger.LogTrace("Dequeued update {UpdateId} of type {UpdateType} from source channel.", update.Id, update.Type);

                        // Add the update to our bounded processing queue.
                        // This operation will block if the _pendingProcessingQueue is full,
                        // thus providing back-pressure to the source channel if processing is slow.
                        if (!_pendingProcessingQueue.IsAddingCompleted)
                        {
                            _pendingProcessingQueue.Add(update, ct);
                        }
                        else
                        {
                            _logger.LogWarning("Processing queue is marked as completed. Cannot add update {UpdateId}. Stopping producer.", update.Id);
                            break; // Stop producing if the queue is completed.
                        }
                    }
                }, stoppingToken).ConfigureAwait(false);
            }
            catch (BrokenCircuitException)
            {
                _logger.LogWarning("Redis circuit is OPEN. Pausing producer for {BreakDuration}.", _redisBreakDuration);
                await Task.Delay(GetJitteredDelay(_redisBreakDuration), stoppingToken).ConfigureAwait(false);
                // The outer ExecuteAsync loop will re-enter the ExecuteAsync block and attempt to poll again.
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Producer task is stopping due to cancellation.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unhandled exception in producer task. Pausing for {Delay} before continuing.", TimeSpan.FromSeconds(10));
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            }
            finally
            {
                // Signal that no more items will be added to the queue.
                // This allows consumers to finish processing remaining items and exit gracefully.
                _pendingProcessingQueue.CompleteAdding();
                _logger.LogInformation("Producer task has finished and marked processing queue as complete.");
            }
        }

        /// <summary>
        /// Consumer task: Continuously consumes updates from the _pendingProcessingQueue and processes them.
        /// Multiple instances of this task run concurrently, limited by _maxConcurrentProcessors.
        /// </summary>
        private async Task ConsumeUpdatesAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Consumer task started. Processing updates from the queue...");
            try
            {
                // GetConsumingEnumerable provides a blocking (or cancellable) enumeration of the collection.
                // It will block if the collection is empty and wait for items, or it will exit
                // gracefully when CompleteAdding() is called and the collection is empty.
                var processingQueueEnumerator = _pendingProcessingQueue.GetConsumingEnumerable(stoppingToken);

                foreach (var update in processingQueueEnumerator)
                {
                    // We've retrieved an update from the processing queue.
                    _logger.LogTrace("Consumer got update {UpdateId} of type {UpdateType} from processing queue.", update.Id, update.Type);

                    // Process the update with retries. Pass the stoppingToken so the processing itself can be cancelled.
                    await ProcessSingleUpdateWithRetries(update, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected when the service is shutting down.
                _logger.LogInformation("Consumer task cancelled. Finishing up.");
            }
            catch (Exception ex)
            {
                // Catch any unexpected exceptions that were not handled by ProcessSingleUpdateWithRetries.
                _logger.LogCritical(ex, "Unhandled exception in consumer task. This consumer will stop.");
                // In a robust system, you might want to restart consumers or report critical failures.
            }
            finally
            {
                _logger.LogInformation("Consumer task finished.");
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
                    // Apply retry policy to the actual update processing.
                    await _processingRetryPolicy.ExecuteAsync(async (context, ct) =>
                    {
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var updateProcessor = scope.ServiceProvider.GetRequiredService<ITelegramUpdateProcessor>();
                        // Pass the stoppingToken to the processor itself, so it can also react to cancellation.
                        await updateProcessor.ProcessUpdateAsync(update, ct).ConfigureAwait(false);
                    }, pollyContext, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // This catch block is for exceptions that _processingRetryPolicy did NOT handle and rethrow.
                    // This means the retries have been exhausted, or the exception type was not configured for retry.
                    _logger.LogError(ex, "Update {UpdateId} failed processing permanently after all retries.", update.Id);
                    // Admin notification logic removed.
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
            _logger.LogInformation("Update Queue Consumer Service stop requested. Waiting for active tasks to complete...");

            // Signal that no more items will be added to the processing queue.
            // This allows the existing consumers to finish processing their current item
            // and then exit cleanly once the queue is empty.
            _pendingProcessingQueue.CompleteAdding();

            // Now, signal the main ExecuteAsync loop to stop.
            // This will eventually cause the producer task to exit and the consumer tasks
            // (which are waiting on GetConsumingEnumerable) to also exit gracefully.

            await base.StopAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Update Queue Consumer Service stop complete.");
        }
        #endregion
    }
}