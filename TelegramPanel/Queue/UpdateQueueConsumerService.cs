// File: TelegramPanel/Queue/UpdateQueueConsumerService.cs
#region Usings
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using TelegramPanel.Application.Interfaces;
using TelegramPanel.Queue.Models; // Ensure this using is present
#endregion

namespace TelegramPanel.Queue
{
    public sealed class UpdateQueueConsumerService : BackgroundService
    {
        #region Core Components
        private readonly ILogger<UpdateQueueConsumerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITelegramUpdateChannel _updateChannel;
        private readonly IQueueMetricsService _metrics;
        private readonly UpdateQueueOptions _options;
        #endregion

        #region Concurrency & State Management
        private readonly BlockingCollection<Update> _workQueue;
        private readonly SemaphoreSlim _concurrencyLimiter;
        private CancellationTokenSource? _serviceStoppingCts;
        private readonly Dictionary<string, Task> _workerTasks = new();
        private int _activeProcessingTasksCount;
        #endregion

        #region Resilience Policies
        // SECURE-FIX: Renamed for clarity, as it's now specifically for the producer.
        private readonly AsyncPolicyWrap _producerResiliencePolicy;
        private readonly AsyncRetryPolicy _processingRetryPolicy;
        #endregion

        #region Constructor
        public UpdateQueueConsumerService(
            ILogger<UpdateQueueConsumerService> logger,
            IServiceScopeFactory scopeFactory,
            ITelegramUpdateChannel updateChannel,
            IQueueMetricsService metrics,
            IOptions<UpdateQueueOptions> options)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _updateChannel = updateChannel;
            _metrics = metrics;
            _options = options.Value;

            _concurrencyLimiter = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);
            _workQueue = new BlockingCollection<Update>(new ConcurrentQueue<Update>(), _options.QueueCapacity);

            _logger.LogInformation(
                "UpdateQueueConsumer initialized. Concurrency: {MaxConcurrency}, QueueCapacity: {QueueCapacity}",
                _options.MaxConcurrency, _options.QueueCapacity);

            #region Polly Policy Setup

            // SECURE-FIX: A much more robust, configurable, and intention-revealing policy setup for the producer.

            // Policy 1: Timeout (Innermost) - For the long-poll operation.
            var producerTimeoutPolicy = Policy.TimeoutAsync(
                _options.ProducerRedisTimeoutSeconds,
                TimeoutStrategy.Optimistic);

            // Policy 2: Circuit Breaker - Protects against a faulty Redis connection.
            // It will only be triggered by actual Redis exceptions, not by timeouts.
            var producerCircuitBreaker = Policy
                .Handle<RedisException>() // Only handle real connection errors
                .Or<RedisConnectionException>()
                .CircuitBreakerAsync(
                    _options.ProducerCircuitBreakerFailures,
                    TimeSpan.FromSeconds(_options.ProducerCircuitBreakSeconds),
                    (ex, ts) => _logger.LogCritical(ex, "PRODUCER circuit breaker opened for {BreakDuration}. The source queue is likely unavailable.", ts),
                    () => _logger.LogInformation("PRODUCER circuit breaker reset. Resuming normal operation."),
                    () => _logger.LogWarning("PRODUCER circuit breaker is in half-open state. Next attempt will test the connection."));

            // Policy 3: Retry (Outermost) - Retries on connection errors with exponential backoff.
            var producerRetryPolicy = Policy
                .Handle<RedisException>()
                .Or<RedisConnectionException>()
                .WaitAndRetryAsync(
                    _options.ProducerMaxRetryAttempts,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, ts, attempt, ctx) => _logger.LogWarning(ex,
                        "Producer failed to connect to source queue. Retrying in {TimeSpan} (Attempt {Attempt}/{MaxAttempts})",
                        ts, attempt, _options.ProducerMaxRetryAttempts));

            // Wrap them together. Execution order is outside-in: Retry -> Circuit Breaker -> Timeout.
            _producerResiliencePolicy = Policy.WrapAsync(producerRetryPolicy, producerCircuitBreaker, producerTimeoutPolicy);

            _processingRetryPolicy = Policy
                .Handle<Exception>(ex => ex is not OperationCanceledException)
                .WaitAndRetryAsync(5,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, ts, attempt, ctx) => _logger.LogWarning(ex,
                        "Processing failed. Retrying in {TimeSpan} (Attempt {Attempt})", ts, attempt));
            #endregion
        }
        #endregion

        #region Main Loop
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Update Queue Consumer Service starting.");
            _serviceStoppingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var ct = _serviceStoppingCts.Token;

            _workerTasks["Producer"] = Task.Run(() => ProducerLoopAsync(ct), ct);
            _workerTasks["Dispatcher"] = Task.Run(() => DispatcherLoopAsync(ct), ct);
            _workerTasks["Metrics"] = Task.Run(() => MetricsLoopAsync(ct), ct);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var completedTask = await Task.WhenAny(_workerTasks.Values);
                    if (completedTask.IsFaulted && !ct.IsCancellationRequested)
                    {
                        var taskName = _workerTasks.FirstOrDefault(kvp => kvp.Value == completedTask).Key ?? "Unknown Task";
                        _logger.LogCritical(completedTask.Exception, "Critical worker task '{TaskName}' has failed. Restarting...", taskName);
                        _workerTasks[taskName] = taskName switch
                        {
                            "Producer" => Task.Run(() => ProducerLoopAsync(ct), ct),
                            "Dispatcher" => Task.Run(() => DispatcherLoopAsync(ct), ct),
                            "Metrics" => Task.Run(() => MetricsLoopAsync(ct), ct),
                            _ => throw new InvalidOperationException("Attempted to restart an unknown worker task.")
                        };
                    }
                    await Task.Delay(_options.SupervisorLoopIntervalMs, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "An unexpected error occurred in the supervisor loop.");
                }
            }
            _logger.LogInformation("Supervisor loop has ended.");
        }

        #endregion

        #region Core Loops

        private async Task ProducerLoopAsync(CancellationToken ct)
        {
            _logger.LogInformation("Producer loop started. Polling source queue for updates.");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // SECURE-FIX: The resilience policy now correctly handles everything internally.
                    // We just need to catch the specific exceptions Polly throws to control the loop.
                    await _producerResiliencePolicy.ExecuteAsync(async token =>
                    {
                        await foreach (var update in _updateChannel.ReadAllAsync(token).WithCancellation(token))
                        {
                            if (update != null) _workQueue.Add(update, token);
                        }
                    }, ct);
                }
                // SECURE-FIX: Handle specific, expected exceptions gracefully.
                catch (TimeoutRejectedException)
                {
                    // This is NORMAL. It means the long-poll timed out without a message.
                    // We log it at a low level (Trace) and immediately loop again to re-poll.
                    _logger.LogTrace("Producer poll timed out as expected. Re-polling immediately.");
                    continue; // No delay needed.
                }
                catch (BrokenCircuitException)
                {
                    // The circuit is open. Polly has already logged this. We just wait before trying again.
                    // The delay prevents a tight loop while the source (Redis) is down.
                    _logger.LogWarning("Producer is paused due to an open circuit. Will check again in {Delay}s.", _options.ProducerCircuitBreakSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.ProducerCircuitBreakSeconds), ct);
                }
                catch (OperationCanceledException)
                {
                    // The service is shutting down.
                    break;
                }
                catch (Exception ex)
                {
                    // Catch any other unexpected, genuine errors from the producer logic.
                    // This is now for TRUE errors, not timeouts.
                    _logger.LogError(ex, "Producer loop encountered an unhandled critical error. Retrying after a delay...");
                    await Task.Delay(5000, ct); // A small delay before retrying a genuine failure.
                }
            }
            _logger.LogInformation("Producer loop finished.");
        }
        #endregion

        #region Core Loops

        private async Task DispatcherLoopAsync(CancellationToken ct)
        {
            _logger.LogInformation("Dispatcher loop started.");

            // The loop continues as long as the service is running OR there's still work in the queue.
            // This ensures the queue is drained during shutdown.
            while (!ct.IsCancellationRequested || _workQueue.Count > 0)
            {
                try
                {
                    var update = _workQueue.Take(ct); // Blocks until an item is available or cancelled

                    await _concurrencyLimiter.WaitAsync(ct);

                    Interlocked.Increment(ref _activeProcessingTasksCount);

                    // Fire-and-forget the processing. The task handles its own lifecycle,
                    // including decrementing the counter and releasing the semaphore.
                    _ = Task.Run(() => ProcessUpdateAndCleanupAsync(update, ct), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (InvalidOperationException) { break; } // Thrown by Take if queue is completed and empty
            }
            _logger.LogInformation("Dispatcher loop finished.");
        }

        private async Task MetricsLoopAsync(CancellationToken ct)
        {
            _logger.LogInformation("Metrics reporting loop started.");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _metrics.UpdateQueueDepth(_workQueue.Count);
                    _metrics.UpdateConcurrency(_options.MaxConcurrency - _concurrencyLimiter.CurrentCount, _options.MaxConcurrency);
                    await _metrics.ReportMetricsAsync(ct);

                    // UPGRADED: Delay is now from configuration
                    await Task.Delay(TimeSpan.FromSeconds(_options.MetricsReportIntervalSeconds), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Metrics reporting failed.");
                }
            }
            _logger.LogInformation("Metrics reporting loop finished.");
        }

        #endregion

        #region Processing Logic

        private async Task ProcessUpdateAndCleanupAsync(Update update, CancellationToken ct)
        {
            try
            {
                await ProcessSingleUpdateWithRetries(update, ct);
            }
            catch (Exception ex)
            {
                // This catch is a safeguard. The inner method should handle everything.
                _logger.LogCritical(ex, "An untrapped exception occurred during ProcessUpdateAndCleanupAsync for Update {UpdateId}", update.Id);
            }
            finally
            {
                // CRITICAL: Ensure resources are always released and counters updated.
                _concurrencyLimiter.Release();
                Interlocked.Decrement(ref _activeProcessingTasksCount);
            }
        }

        private async Task ProcessSingleUpdateWithRetries(Update update, CancellationToken ct)
        {
            // UPGRADED: Using a logger scope to tag all subsequent logs with the UpdateId.
            using (_logger.BeginScope("UpdateId: {UpdateId}", update.Id))
            {
                var pollyContext = new Context($"UpdateProcessing_{update.Id}");
                try
                {
                    await _processingRetryPolicy.ExecuteAsync(async (context, token) =>
                    {
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var processor = scope.ServiceProvider.GetRequiredService<ITelegramUpdateProcessor>();
                        await processor.ProcessUpdateAsync(update, token);
                    }, pollyContext, ct);

                    _metrics.IncrementProcessed();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _metrics.IncrementFailed();
                    _logger.LogError(ex, "Update processing failed permanently. Moving to Dead Letter Queue.");
                    await MoveToDeadLetterQueueAsync(update, ex);
                }
            }
        }

        private async Task MoveToDeadLetterQueueAsync(Update update, Exception lastException)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
                var payload = JsonSerializer.Serialize(new { FailedUpdate = update, LastException = lastException.Message, TimestampUtc = DateTime.UtcNow });
                await redis.ListRightPushAsync(_options.DeadLetterQueueName, payload);
                _metrics.IncrementDeadLettered();
            }
            catch (Exception dqlEx)
            {
                _logger.LogCritical(dqlEx, "FATAL: Could not move poison message to DLQ.");
            }
        }

        #endregion

        #region Service Lifecycle

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stop requested. Initiating graceful shutdown...");

            // 1. Stop adding new items to the queue.
            //    The dispatcher loop will continue to process existing items.
            _workQueue.CompleteAdding();

            // 2. Wait for all in-flight processing tasks to finish.
            _logger.LogInformation("Draining queue: waiting for {ActiveCount} active tasks to complete.", _activeProcessingTasksCount);
            var drainTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.ShutdownTimeoutSeconds));
            while (_activeProcessingTasksCount > 0 && !drainTimeoutCts.IsCancellationRequested)
            {
                await Task.Delay(100, CancellationToken.None);
            }

            if (_activeProcessingTasksCount > 0)
            {
                _logger.LogWarning("Graceful drain timed out. {RemainingCount} tasks may be terminated.", _activeProcessingTasksCount);
            }
            else
            {
                _logger.LogInformation("All active tasks completed.");
            }

            // 3. Signal the main worker loops to stop.
            if (_serviceStoppingCts != null && !_serviceStoppingCts.IsCancellationRequested)
            {
                _logger.LogInformation("Stopping all background worker loops.");
                _serviceStoppingCts.Cancel();
            }

            // 4. Wait for the main worker tasks (Producer, Dispatcher, Metrics) to finish.
            await Task.WhenAll(_workerTasks.Values);

            _logger.LogInformation("Update Queue Consumer Service has stopped.");
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            base.Dispose();
            _serviceStoppingCts?.Dispose();
            _workQueue?.Dispose();
            _concurrencyLimiter?.Dispose();
        }
        #endregion
    }

}