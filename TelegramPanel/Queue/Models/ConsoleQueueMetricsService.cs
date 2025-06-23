#region Usings
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TelegramPanel.Queue.Models;
#endregion

namespace TelegramPanel.Queue
{
    /// <summary>
    /// A thread-safe, console-focused implementation of IQueueMetricsService.
    /// It provides detailed, real-time reports to the console, including processing rates,
    /// queue depth, and a visual concurrency bar. This implementation is designed for
    /// local development and real-time monitoring via console output.
    /// </summary>
    public sealed class ConsoleQueueMetricsService : IQueueMetricsService
    {
        private readonly ILogger<ConsoleQueueMetricsService> _logger;

        // Use Interlocked for all counter operations to ensure thread safety.
        private long _processedCount;
        private long _failedCount;
        private long _deadLetteredCount;
        private long _currentQueueDepth;
        private (int current, int max) _concurrency;

        // Fields for rate calculation
        private long _lastProcessedCount;
        private DateTime _lastReportTime = DateTime.UtcNow;

        public ConsoleQueueMetricsService(ILogger<ConsoleQueueMetricsService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Public Methods (Interface Implementation)

        /// <inheritdoc/>
        public void IncrementProcessed() => Interlocked.Increment(ref _processedCount);

        /// <inheritdoc/>
        public void IncrementFailed() => Interlocked.Increment(ref _failedCount);

        /// <inheritdoc/>
        public void IncrementDeadLettered() => Interlocked.Increment(ref _deadLetteredCount);

        /// <inheritdoc/>
        public void UpdateQueueDepth(long depth) => Interlocked.Exchange(ref _currentQueueDepth, depth);

        /// <inheritdoc/>
        public void UpdateConcurrency(int current, int max) => _concurrency = (current, max);

        /// <inheritdoc/>
        public async Task ReportMetricsAsync(CancellationToken stoppingToken)
        {
            // --- Calculate deltas for rate ---
            var now = DateTime.UtcNow;
            var elapsed = now - _lastReportTime;

            // Read current values atomically for a consistent snapshot
            long currentProcessed = Interlocked.Read(ref _processedCount);
            long currentFailed = Interlocked.Read(ref _failedCount);
            long currentDeadLettered = Interlocked.Read(ref _deadLetteredCount);
            long queueDepth = Interlocked.Read(ref _currentQueueDepth);

            long processedInPeriod = currentProcessed - _lastProcessedCount;

            // Avoid division by zero on the first run or if the interval is too short
            double ratePerSecond = elapsed.TotalSeconds > 1 ? processedInPeriod / elapsed.TotalSeconds : 0;

            // --- Build the rich log message ---
            var reportBuilder = new StringBuilder();
            const int labelWidth = 20;

            reportBuilder.AppendLine();
            reportBuilder.AppendLine("----- Queue Metrics Report -----");
            reportBuilder.AppendLine($"{"Processing Rate:",-labelWidth}{ratePerSecond:F2} updates/sec");
            reportBuilder.AppendLine($"{"Concurrency:",-labelWidth}{BuildConcurrencyBar(_concurrency.current, _concurrency.max)} {_concurrency.current}/{_concurrency.max}");
            reportBuilder.AppendLine($"{"Queue Depth:",-labelWidth}{queueDepth:N0} items");
            reportBuilder.AppendLine("--------------------------------");
            reportBuilder.AppendLine($"{"Total Processed:",-labelWidth}{currentProcessed:N0}");
            reportBuilder.AppendLine($"{"Total Failed:",-labelWidth}{currentFailed:N0}");
            reportBuilder.AppendLine($"{"Total Dead-Lettered:",-labelWidth}{currentDeadLettered:N0}");
            reportBuilder.Append("--------------------------------");

            // --- Log with appropriate level ---
            // If there are any failures or the queue is backed up, log as a warning.
            bool hasWarningState = currentFailed > 0 || currentDeadLettered > 0 || queueDepth > (_concurrency.max * 2);
            LogLevel level = hasWarningState ? LogLevel.Warning : LogLevel.Information;

            _logger.Log(level, reportBuilder.ToString());

            // --- Update state for the next report ---
            _lastReportTime = now;
            _lastProcessedCount = currentProcessed;

            // This method is called within a loop in the consumer service, so no delay here.
            await Task.CompletedTask;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Creates a visual text-based progress bar.
        /// Example: [██████████░░░░░░░░░░] 10/20
        /// </summary>
        private static string BuildConcurrencyBar(int current, int max)
        {
            if (max <= 0) return "[ N/A ]";

            const int barWidth = 20;
            double percentage = Math.Clamp((double)current / max, 0.0, 1.0);
            int filledBlocks = (int)(percentage * barWidth);
            int emptyBlocks = barWidth - filledBlocks;

            return $"[{new string('█', filledBlocks)}{new string('░', emptyBlocks)}]";
        }

        #endregion
    }
}