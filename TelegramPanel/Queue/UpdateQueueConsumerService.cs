// File: TelegramPanel/Queue/UpdateQueueConsumerService.cs
#region Usings
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
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
        private CancellationTokenSource _serviceStoppingCts;
        private readonly Dictionary<string, Task> _workerTasks = new(); // UPGRADED: Dictionary for named tasks
        private int _activeProcessingTasksCount; // UPGRADED: Efficient counter for in-flight work
        #endregion

        #region Resilience Policies
        private readonly AsyncPolicyWrap _redisResiliencePolicy;
        private readonly AsyncRetryPolicy _processingRetryPolicy;
        #endregion

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
            var redisCircuitBreaker = Policy
                .Handle<RedisException>()
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(60),
                    (ex, ts) => _logger.LogCritical(ex, "Redis circuit breaker opened for {BreakDuration}", ts),
                    () => _logger.LogInformation("Redis circuit breaker reset"));

            _redisResiliencePolicy = Policy.WrapAsync(redisCircuitBreaker, Policy.TimeoutAsync(30));

            _processingRetryPolicy = Policy
                .Handle<Exception>(ex => ex is not OperationCanceledException)
                .WaitAndRetryAsync(5,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, ts, attempt, ctx) => _logger.LogWarning(ex,
                        "Processing failed. Retrying in {TimeSpan} (Attempt {Attempt})", ts, attempt)); // Scope provides UpdateId
            #endregion
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Update Queue Consumer Service starting.");
            _serviceStoppingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var ct = _serviceStoppingCts.Token;

            // UPGRADED: Start and name core worker tasks
            _workerTasks["Producer"] = Task.Run(() => ProducerLoopAsync(ct), ct);
            _workerTasks["Dispatcher"] = Task.Run(() => DispatcherLoopAsync(ct), ct);
            _workerTasks["Metrics"] = Task.Run(() => MetricsLoopAsync(ct), ct);

            // UPGRADED: Supervisor loop with named task restarting
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var completedTask = await Task.WhenAny(_workerTasks.Values);

                    if (completedTask.IsFaulted && !ct.IsCancellationRequested)
                    {
                        // Find the name of the faulted task
                        var taskName = _workerTasks.FirstOrDefault(kvp => kvp.Value == completedTask).Key ?? "Unknown Task";

                        _logger.LogCritical(completedTask.Exception, "Critical worker task '{TaskName}' has failed. Restarting...", taskName);

                        // Restart the specific task that failed
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
                catch (OperationCanceledException) { break; } // Expected on shutdown
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "An unexpected error occurred in the supervisor loop.");
                }
            }
            _logger.LogInformation("Supervisor loop has ended.");
        }

        #region Core Loops

        private async Task ProducerLoopAsync(CancellationToken ct)
        {
            _logger.LogInformation("Producer loop started.");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await _redisResiliencePolicy.ExecuteAsync(async token =>
                    {
                        await foreach (var update in _updateChannel.ReadAllAsync(token).WithCancellation(token))
                        {
                            if (update != null) _workQueue.Add(update, token);
                        }
                    }, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Producer loop encountered a critical error. Retrying after a delay...");
                    await Task.Delay(5000, ct);
                }
            }
            _logger.LogInformation("Producer loop finished.");
        }

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