// --- START OF FILE: RedisUpdateChannel.cs ---

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using StackExchange.Redis;
using System.Diagnostics; // Required for Process.GetCurrentProcess()
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
    private readonly string _consumerId; // Renamed for clarity

    public RedisUpdateChannel(
        IConnectionMultiplexer redis,
        ILogger<RedisUpdateChannel> logger,
        IOptions<UpdateQueueOptions> options)
    {
        _logger = logger;
        _redisDb = redis.GetDatabase();

        UpdateQueueOptions queueOptions = options.Value;
        _mainQueueKey = queueOptions.QueueName;
        _deadLetterQueueKey = queueOptions.DeadLetterQueueName;

        // --- START OF FIX ---
        // Create a STABLE consumer ID based on the machine and process.
        // This ensures that if the application restarts, it gets a new ID,
        // but a single running instance always uses the same ID.
        // In a containerized environment (like Docker/Kubernetes), the machine name (pod name) will be unique.
        var processId = Process.GetCurrentProcess().Id;
        var machineName = Environment.MachineName;
        _consumerId = $"{machineName}-{processId}";
        // --- END OF FIX ---

        _processingQueueKey = $"{queueOptions.QueueName}:processing:{_consumerId}";

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
                // --- FIX: Ensure TypeNameHandling is used for robust deserialization ---
                // This helps when deserializing complex types like Update that have subtypes.
                string jsonUpdate = JsonConvert.SerializeObject(update, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });
                await _redisDb.ListLeftPushAsync(_mainQueueKey, jsonUpdate);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to serialize Telegram update {UpdateId}.", update.Id);
            }
        }, cancellationToken);
    }


    public async IAsyncEnumerable<QueueMessage> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting reliable consumer on Redis queue '{QueueKey}' for Consumer '{ConsumerId}'.", _mainQueueKey, _consumerId);

        while (!cancellationToken.IsCancellationRequested)
        {
            RedisValue redisValue;
            try
            {
                redisValue = await _redisDb.ListRightPopLeftPushAsync(_mainQueueKey, _processingQueueKey);
            }
            catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException or OperationCanceledException)
            {
                if (ex is OperationCanceledException)
                {
                    _logger.LogInformation("CancellationToken requested. Shutting down Redis consumer loop.");
                    break;
                }

                _logger.LogError(ex, "Connection to Redis lost while popping from queue. Retrying in 5 seconds...");
                await Task.Delay(5000, CancellationToken.None); // Use CancellationToken.None to ensure delay completes
                continue;
            }

            if (redisValue.HasValue)
            {
                Update? update;
                try
                {
                    // --- FIX: Use matching TypeNameHandling for deserialization ---
                    update = JsonConvert.DeserializeObject<Update>(redisValue!, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize update from Redis. Moving to dead-letter queue. JSON: {Json}", redisValue.ToString());
                    await _redisDb.ListMoveAsync(_processingQueueKey, _deadLetterQueueKey, ListSide.Left, ListSide.Left);
                    continue;
                }

                if (update == null)
                {
                    _logger.LogError("Deserialization resulted in a null Update object. Moving to dead-letter queue. JSON: {Json}", redisValue.ToString());
                    await _redisDb.ListMoveAsync(_processingQueueKey, _deadLetterQueueKey, ListSide.Left, ListSide.Left);
                    continue;
                }

                yield return new QueueMessage(update, redisValue);
            }
            else
            {
                // No item was found, wait a bit before trying again.
                await Task.Delay(200, cancellationToken);
            }
        }
        _logger.LogInformation("Stopped consuming from Redis queue '{QueueKey}' for Consumer '{ConsumerId}'.", _mainQueueKey, _consumerId);
    }


    public async Task AcknowledgeAsync(QueueMessage message, CancellationToken cancellationToken = default)
    {
        // Remove one occurrence of the message from the processing queue.
        long removedCount = await _redisDb.ListRemoveAsync(_processingQueueKey, message.RawValue, 1);
        if (removedCount > 0)
        {
            _logger.LogTrace("Acknowledged message for UpdateId {UpdateId}.", message.DeserializedUpdate?.Id);
        }
        else
        {
            _logger.LogWarning("Attempted to acknowledge message for UpdateId {UpdateId}, but it was not found in the processing queue '{QueueKey}'. It might have been re-queued by a janitor.",
               message.DeserializedUpdate?.Id, _processingQueueKey);
        }
    }


    // --- INSIDE RedisUpdateChannel.cs ---

    public async Task RequeueAsync(QueueMessage message, CancellationToken cancellationToken = default)
    {
        // Atomically move the message from our processing queue back to the head of the main queue.
        // The method returns the RedisValue of the item that was moved.
        // If the source list was empty, the returned RedisValue.HasValue will be false.

        // --- START OF FINAL FIX ---
        RedisValue movedValue = await _redisDb.ListMoveAsync(_processingQueueKey, _mainQueueKey, ListSide.Left, ListSide.Left);

        // Check if a value was actually moved.
        if (movedValue.HasValue)
        // --- END OF FINAL FIX ---
        {
            _logger.LogWarning("Re-queued message for UpdateId {UpdateId} for reprocessing.", message.DeserializedUpdate?.Id);
        }
        else
        {
            // This case is rare but could happen if another process (like a janitor)
            // moved the message just before this method was called, or if the queue was empty.
            _logger.LogWarning("Attempted to re-queue message for UpdateId {UpdateId}, but it was no longer in the processing queue '{Queue}'.",
                message.DeserializedUpdate?.Id, _processingQueueKey);
        }
    }
}