using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Telegram.Bot.Types;
using TelegramPanel.Queue.Models;

namespace TelegramPanel.Queue
{
    // No change needed for ITelegramUpdateChannel.cs
    public interface ITelegramUpdateChannel
    {
        ValueTask WriteAsync(Update update, CancellationToken cancellationToken = default);
        IAsyncEnumerable<QueueMessage> ReadAllAsync(CancellationToken cancellationToken = default);
        Task AcknowledgeAsync(QueueMessage message, CancellationToken cancellationToken = default);
        // --- NEW: Method to requeue a message that failed processing but should be retried ---
        Task RequeueAsync(QueueMessage message, CancellationToken cancellationToken = default);

    }
    public class TelegramUpdateChannel : ITelegramUpdateChannel
    {
        private readonly Channel<Update> _channel;
        private readonly ILogger<TelegramUpdateChannel> _logger;
        private readonly AsyncRetryPolicy _writeRetryPolicy;
        private readonly AsyncRetryPolicy<Update> _readRetryPolicy;
        private readonly int _maxRetryAttempts;

        public TelegramUpdateChannel(ILogger<TelegramUpdateChannel> logger, int capacity = 50000)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxRetryAttempts = 3;

            BoundedChannelOptions options = new(capacity)
            {
                // ✅ تغییر مهم: بازگشت به Wait. این تضمین می‌کند که هیچ آپدیتی در صف داخلی از دست نمی‌رود.
                // مدیریت بلاک شدن این WriteAsync (اگر Channel پر باشد) به لایه فراخواننده (TelegramBotService) واگذار می‌شود.
                FullMode = BoundedChannelFullMode.Wait, // ✅ بازگشت به Wait
                SingleReader = false,
                SingleWriter = false
            };
            _channel = Channel.CreateBounded<Update>(options);

            _writeRetryPolicy = Policy
                .Handle<Exception>(ex => ex is not (OperationCanceledException or TaskCanceledException))
                .WaitAndRetryAsync(
                    retryCount: _maxRetryAttempts,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryAttempt, context) =>
                    {
                        _logger.LogWarning(exception,
                            "TelegramUpdateChannel: WriteAsync failed. Retrying in {TimeSpan} (attempt {RetryAttempt}/{MaxRetries}). Error: {Message}",
                            timeSpan, retryAttempt, _maxRetryAttempts, exception.Message);
                    });

            _readRetryPolicy = Policy<Update>
                .Handle<Exception>(ex => ex is not (OperationCanceledException or TaskCanceledException))
                .WaitAndRetryAsync(
                    retryCount: _maxRetryAttempts,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (delegateResult, timeSpan, retryAttempt, context) =>
                    {
                        _logger.LogWarning(delegateResult.Exception,
                            "TelegramUpdateChannel: ReadAsync failed. Retrying in {TimeSpan} (attempt {RetryAttempt}/{MaxRetries}). Error: {Message}",
                            timeSpan, retryAttempt, _maxRetryAttempts, delegateResult.Exception?.Message);
                    });

            _logger.LogInformation("TelegramUpdateChannel initialized with capacity {Capacity} and FullMode '{FullMode}'.", capacity, options.FullMode);
        }


        public async IAsyncEnumerable<QueueMessage> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (Update update in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                // Wrap the Update in a QueueMessage. The raw value is null because there's no Redis value.
                yield return new QueueMessage(update, default);
            }
        }
        public Task RequeueAsync(QueueMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Re-queue called for in-memory queue. Re-writing message to channel.");
            if (message.DeserializedUpdate != null)
            {
                // To properly simulate a requeue, we can try to write it back.
                _ = WriteAsync(message.DeserializedUpdate, cancellationToken);
            }
            return Task.CompletedTask;
        }
        public Task AcknowledgeAsync(QueueMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogTrace("Acknowledge called for in-memory queue. No action taken.");
            return Task.CompletedTask;
        }


        public async ValueTask WriteAsync(Update update, CancellationToken cancellationToken = default)
        {
            // اکنون این فراخوانی ممکن است منتظر بماند اگر Channel پر باشد.
            // مدیریت Timeout و انتقال به Hangfire در لایه بالاتر (TelegramBotService) انجام می‌شود.
            await _writeRetryPolicy.ExecuteAsync(async () =>
            {
                await _channel.Writer.WriteAsync(update, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public async ValueTask<Update> ReadAsync(CancellationToken cancellationToken = default)
        {
            return await _readRetryPolicy.ExecuteAsync(async () =>
            {
                return await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }


    }
}