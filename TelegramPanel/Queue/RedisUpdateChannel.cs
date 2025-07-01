// --- START OF FULLY CORRECTED FILE: RedisUpdateChannel.cs ---

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using StackExchange.Redis;
using System.Runtime.CompilerServices;
using Telegram.Bot.Types;
using TelegramPanel.Queue;
using TelegramPanel.Queue.Models;

public class RedisUpdateChannel : ITelegramUpdateChannel
{
    private readonly ILogger<RedisUpdateChannel> _logger;
    private readonly IDatabase _redisDb;
    private readonly AsyncRetryPolicy _redisRetryPolicy;
    private readonly string _mainQueueKey;
    private readonly string _processingQueueKey;
    private readonly string _deadLetterQueueKey;
    private readonly string _consumerId = Guid.NewGuid().ToString("N");

    public RedisUpdateChannel(
        IConnectionMultiplexer redis,
        ILogger<RedisUpdateChannel> logger,
        IOptions<UpdateQueueOptions> options)
    {
        _logger = logger;
        _redisDb = redis.GetDatabase();

        UpdateQueueOptions queueOptions = options.Value;
        _mainQueueKey = queueOptions.QueueName;
        _processingQueueKey = $"{queueOptions.QueueName}:processing:{_consumerId}";
        _deadLetterQueueKey = queueOptions.DeadLetterQueueName;

        _redisRetryPolicy = Policy
            .Handle<RedisConnectionException>().Or<RedisTimeoutException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "Redis operation failed. Retrying in {TimeSpan}s. Attempt {RetryCount}/3.", timeSpan.TotalSeconds, retryCount);
                });

        _logger.LogInformation("RedisUpdateChannel initialized for consumer '{ConsumerId}'. MainQueue: '{MainQueue}', ProcessingQueue: '{ProcessingQueue}'",
            _consumerId, _mainQueueKey, _processingQueueKey);
    }

    public async ValueTask WriteAsync(Update update, CancellationToken cancellationToken = default)
    {
        await _redisRetryPolicy.ExecuteAsync(async token =>
        {
            try
            {
                string jsonUpdate = JsonConvert.SerializeObject(update);
                _ = await _redisDb.ListLeftPushAsync(_mainQueueKey, jsonUpdate);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to serialize Telegram update {UpdateId}.", update.Id);
            }
        }, cancellationToken);
    }


    public async IAsyncEnumerable<QueueMessage> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting reliable consumer on Redis queue '{QueueKey}'.", _mainQueueKey);

        while (!cancellationToken.IsCancellationRequested)
        {
            RedisValue redisValue;
            try
            {
                redisValue = await _redisDb.ListRightPopLeftPushAsync(_mainQueueKey, _processingQueueKey);
            }
            catch (Exception ex) when (ex is RedisConnectionException or OperationCanceledException)
            {
                if (ex is OperationCanceledException)
                {
                    break;
                }

                _logger.LogError(ex, "Connection to Redis lost while popping from queue. Retrying...");
                await Task.Delay(2000, cancellationToken);
                continue;
            }

            if (redisValue.HasValue)
            {
                Update? update;
                try
                {
                    update = JsonConvert.DeserializeObject<Update>(redisValue!);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize update from Redis. Moving to dead-letter queue. JSON: {Json}", redisValue.ToString());

                    // --- FIX APPLIED HERE ---
                    // The correct enum is ListSide, not RedisSide.
                    _ = await _redisDb.ListMoveAsync(_processingQueueKey, _deadLetterQueueKey, ListSide.Left, ListSide.Left);
                    continue;
                }

                yield return new QueueMessage(update, redisValue);
            }
            else
            {
                await Task.Delay(200, cancellationToken);
            }
        }
        _logger.LogInformation("Stopped consuming from Redis queue '{QueueKey}'.", _mainQueueKey);
    }


    public async Task AcknowledgeAsync(QueueMessage message, CancellationToken cancellationToken = default)
    {
        _ = await _redisDb.ListRemoveAsync(_processingQueueKey, message.RawValue, 1);
        _logger.LogTrace("Acknowledged message for UpdateId {UpdateId}.", message.DeserializedUpdate?.Id);
    }


    // In RedisUpdateChannel.cs

    public async Task RequeueAsync(QueueMessage message, CancellationToken cancellationToken = default)
    {
        // Atomically move the message from our processing queue back to the head of the main queue.
        // We move from the left of the processing queue to the left of the main queue.

        // --- FIX APPLIED HERE ---
        // The ListMoveAsync method returns the RedisValue of the item that was moved,
        // or a null RedisValue if the source list was empty.
        // We check if the returned value has a value to confirm the move was successful.
        RedisValue movedValue = await _redisDb.ListMoveAsync(_processingQueueKey, _mainQueueKey, ListSide.Left, ListSide.Left);

        if (movedValue.HasValue)
        {
            _logger.LogWarning("Re-queued message for UpdateId {UpdateId} for reprocessing.", message.DeserializedUpdate?.Id);
        }
        else
        {
            // This case is rare but could happen if another process (like a janitor)
            // moved the message just before this method was called. It's good to log it.
            _logger.LogWarning("Attempted to re-queue message for UpdateId {UpdateId}, but it was no longer in the processing queue '{Queue}'.",
                message.DeserializedUpdate?.Id, _processingQueueKey);
        }
    }
}