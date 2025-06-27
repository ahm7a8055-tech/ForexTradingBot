// In a new file, e.g., Infrastructure/Queue/RedisUpdateChannel.cs
using StackExchange.Redis;
using System.Text.Json;
using Polly;
using Polly.Retry;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using TelegramPanel.Queue;
using Telegram.Bot.Types;

public class RedisUpdateChannel : ITelegramUpdateChannel
{
    private readonly ILogger<RedisUpdateChannel> _logger;
    private readonly IDatabase _redisDb;
    private readonly AsyncRetryPolicy _redisRetryPolicy;
    private const string UpdateQueueKey = "queue:telegram:updates";

    public RedisUpdateChannel(IConnectionMultiplexer redis, ILogger<RedisUpdateChannel> logger)
    {
        _logger = logger;
        _redisDb = redis.GetDatabase();

        // Polly policy for handling transient Redis connection issues
        _redisRetryPolicy = Policy
            .Handle<RedisConnectionException>()
            .Or<RedisTimeoutException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "Redis operation failed. Retrying in {TimeSpan}s. Attempt {RetryCount}/3.", timeSpan, retryCount);
                });
    }

    public async ValueTask WriteAsync(Update update, CancellationToken cancellationToken = default)
    {
        await _redisRetryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                // Serialize the Update object to JSON
                var jsonUpdate = JsonSerializer.Serialize(update);

                // LPUSH the JSON string to the head of the Redis List
                await _redisDb.ListLeftPushAsync(UpdateQueueKey, jsonUpdate);

                _logger.LogTrace("Enqueued update {UpdateId} to Redis queue '{QueueKey}'.", update.Id, UpdateQueueKey);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to serialize Telegram update {UpdateId}.", update.Id);
                // Do not re-throw; we don't want to retry a serialization error.
            }
        });
    }

    public async IAsyncEnumerable<Update> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting to consume from Redis queue '{QueueKey}'.", UpdateQueueKey);
        while (!cancellationToken.IsCancellationRequested)
        {
            Update? update = null;
            RedisValue redisValue = default;
            await _redisRetryPolicy.ExecuteAsync(async () =>
            {
                redisValue = await _redisDb.ListRightPopAsync(UpdateQueueKey);
                if (redisValue.HasValue)
                {
                    try
                    {
                        // Defensive: catch KeyNotFoundException for missing polymorphic keys
                        update = JsonSerializer.Deserialize<Update>(redisValue!);
                    }
                    catch (KeyNotFoundException knfEx)
                    {
                        _logger.LogError(knfEx, "KeyNotFoundException during Update deserialization. JSON: {Json}", redisValue.ToString());
                        // Move to dead-letter queue for inspection
                        await _redisDb.ListLeftPushAsync($"{UpdateQueueKey}:deadletter", redisValue);
                        update = null;
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "JsonException during Update deserialization. JSON: {Json}", redisValue.ToString());
                        // Move malformed message to a dead-letter queue for inspection
                        await _redisDb.ListLeftPushAsync($"{UpdateQueueKey}:deadletter", redisValue);
                        update = null;
                    }
                }
            });

            if (update != null)
            {
                yield return update;
            }
            else
            {
                // If BRPOP times out (queue is empty), wait a short moment before trying again
                await Task.Delay(100, cancellationToken);
            }
        }
        _logger.LogInformation("Stopped consuming from Redis queue '{QueueKey}'.", UpdateQueueKey);
    }
}