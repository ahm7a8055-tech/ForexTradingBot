// --- START OF REFACTORED FILE: UpdateQueueConsumerService.cs ---
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System.Collections.Concurrent;
using Telegram.Bot.Types;
using TelegramPanel.Application.Interfaces;
using TelegramPanel.Queue.Models;
using TelegramPanel.Queue.Models.Interface;

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
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly ConcurrentDictionary<Guid, Task> _workerTasks = new();
        #endregion

        #region Resilience Policies
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

            _logger.LogInformation(
                "UpdateQueueConsumer initialized. Concurrency: {MaxConcurrency}",
                _options.MaxConcurrency);

            _processingRetryPolicy = Policy
                  .Handle<Exception>(ex => ex is not OperationCanceledException)
                  .WaitAndRetryAsync(5,
                      retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                      (ex, ts, attempt, ctx) => _logger.LogWarning(ex,
                          "Processing failed. Retrying in {TimeSpan} (Attempt {Attempt})", ts, attempt));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Update Queue Consumer Service starting.");
            Task metricsTask = Task.Run(() => MetricsLoopAsync(stoppingToken), stoppingToken);

            // --- REFACTORED: The only loop we need ---
            await DispatcherLoopAsync(stoppingToken);

            // Wait for metrics to stop and any remaining tasks to finish
            await Task.WhenAll(_workerTasks.Values.Append(metricsTask));
            _logger.LogInformation("Update Queue Consumer Service has stopped.");
        }

        private async Task DispatcherLoopAsync(CancellationToken ct)
        {
            _logger.LogInformation("Dispatcher loop started, consuming from ITelegramUpdateChannel.");

            await foreach (QueueMessage? queueMessage in _updateChannel.ReadAllAsync(ct).WithCancellation(ct))
            {
                try
                {
                    await _concurrencyLimiter.WaitAsync(ct);

                    Guid taskId = Guid.NewGuid();
                    Task processingTask = Task.Run(() => ProcessUpdateAndCleanupAsync(taskId, queueMessage, ct), ct);
                    _ = _workerTasks.TryAdd(taskId, processingTask);
                }
                catch (OperationCanceledException) { break; }
            }
            _logger.LogInformation("Dispatcher loop finished.");
        }

        private async Task ProcessUpdateAndCleanupAsync(Guid taskId, QueueMessage queueMessage, CancellationToken ct)
        {
            try
            {
                await ProcessSingleUpdateWithRetries(queueMessage, ct);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "An untrapped exception occurred during ProcessUpdateAndCleanupAsync for Update {UpdateId}", queueMessage.DeserializedUpdate?.Id);
            }
            finally
            {
                _ = _concurrencyLimiter.Release();
                _ = _workerTasks.TryRemove(taskId, out _);
            }
        }

        private async Task ProcessSingleUpdateWithRetries(QueueMessage queueMessage, CancellationToken ct)
        {
            if (queueMessage.DeserializedUpdate is null)
            {
                _logger.LogError("Received a queue message with a null update. Acknowledging to discard. RawValue: {RawValue}", queueMessage.RawValue.ToString());
                await _updateChannel.AcknowledgeAsync(queueMessage, ct);
                return;
            }

            Update update = queueMessage.DeserializedUpdate;
            using IDisposable? logScope = _logger.BeginScope("UpdateId: {UpdateId}", update.Id);

            try
            {
                await _processingRetryPolicy.ExecuteAsync(async (context, token) =>
                {
                    await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                    ITelegramUpdateProcessor processor = scope.ServiceProvider.GetRequiredService<ITelegramUpdateProcessor>();
                    await processor.ProcessUpdateAsync(update, token);
                }, new Context($"UpdateProcessing_{update.Id}"), ct);

                await _updateChannel.AcknowledgeAsync(queueMessage, ct);
                _metrics.IncrementProcessed();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _metrics.IncrementFailed();
                _logger.LogError(ex, "Update processing failed permanently. Re-queueing for a later attempt.");
                await _updateChannel.RequeueAsync(queueMessage, ct);
            }
        }

        private async Task MetricsLoopAsync(CancellationToken ct)
        {
            _logger.LogInformation("Metrics reporting loop started.");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // For metrics, we can peek at the queue length without consuming
                    // This requires a new method on the interface if you need it, or we can just report concurrency
                    _metrics.UpdateConcurrency(_options.MaxConcurrency - _concurrencyLimiter.CurrentCount, _options.MaxConcurrency);
                    await _metrics.ReportMetricsAsync(ct);
                    await Task.Delay(TimeSpan.FromSeconds(_options.MetricsReportIntervalSeconds), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Metrics reporting failed."); }
            }
            _logger.LogInformation("Metrics reporting loop finished.");
        }

        public override void Dispose()
        {
            base.Dispose();
            _concurrencyLimiter?.Dispose();
        }
    }
}