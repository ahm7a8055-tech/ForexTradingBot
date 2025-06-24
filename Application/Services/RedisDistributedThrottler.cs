// File: Infrastructure/Services/RedisDistributedThrottler.cs
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Polly;
using Polly.Retry;

namespace Infrastructure.Services
{
    /// <summary>
    /// Implements a distributed throttler using Redis and a sliding window log algorithm via a Lua script.
    /// This ensures that operations across multiple distributed workers can be limited to a global rate (e.g., 30 calls per second).
    /// </summary>
    public class RedisDistributedThrottler : IDistributedThrottler
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisDistributedThrottler> _logger;
        private readonly AsyncRetryPolicy _redisRetryPolicy;

        // Lua script for a sliding window log. It cleans old entries, checks the count, and only adds a new entry if under the limit.
        // It returns the time to wait in milliseconds if the limit is exceeded.
        private const string ThrottleLuaScript =
            @"-- KEYS[1]: The unique key for the rate limit sorted set (e.g., 'throttle:telegram-api')
              -- ARGV[1]: The current unix timestamp in milliseconds
              -- ARGV[2]: The time window for the limit in milliseconds
              -- ARGV[3]: The maximum number of requests allowed in the window
              -- ARGV[4]: A unique identifier for the current request (e.g., a guid) to avoid collisions

              local key = KEYS[1]
              local now = tonumber(ARGV[1])
              local window = tonumber(ARGV[2])
              local limit = tonumber(ARGV[3])
              local member = ARGV[4]

              local clear_before = now - window
              -- Remove all timestamps that are outside the current window
              redis.call('ZREMRANGEBYSCORE', key, '-inf', clear_before)

              -- Get the current number of requests in the window
              local current_count = redis.call('ZCARD', key)

              if current_count < limit then
                  -- If under the limit, add the new request and set expiration
                  redis.call('ZADD', key, now, member)
                  -- Set expiration to the window size plus a small buffer to avoid race conditions
                  redis.call('EXPIRE', key, math.ceil(window / 1000) + 1)
                  return 0 -- Success, no wait time
              else
                  -- If at or over the limit, find the oldest timestamp to calculate wait time
                  local oldest_entry = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
                  if oldest_entry and #oldest_entry >= 2 then
                      local oldest_timestamp = tonumber(oldest_entry[2])
                      -- Wait time is the difference between when the oldest request expires and now
                      return (oldest_timestamp + window) - now
                  end
                  -- Fallback wait time if something goes wrong
                  return window / limit
              end";

        public RedisDistributedThrottler(IConnectionMultiplexer redis, ILogger<RedisDistributedThrottler> logger)
        {
            _redis = redis;
            _logger = logger;
            _redisRetryPolicy = Policy
                .Handle<RedisException>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(100 * retryAttempt),
                    (ex, ts, count, ctx) => _logger.LogWarning(ex, "Redis error in throttler. Retry {Count}", count));
        }

        public async Task WaitAsync(string throttleKey, int limit, TimeSpan window, CancellationToken cancellationToken)
        {
            IDatabase db = _redis.GetDatabase();
            var key = $"throttle:{throttleKey}";

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var waitTimeMs = await _redisRetryPolicy.ExecuteAsync<long>(async () =>
                    {
                        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        var result = await db.ScriptEvaluateAsync(ThrottleLuaScript,
                            new RedisKey[] { key },
                            new RedisValue[] { now, (long)window.TotalMilliseconds, limit, Guid.NewGuid().ToString() });
                        return (long)result;
                    });

                    if (waitTimeMs <= 0)
                    {
                        // Permit acquired
                        return;
                    }

                    if (waitTimeMs > 0)
                    {
                        // Limit reached, wait for the calculated duration
                        _logger.LogTrace("Global throttle limit reached for key {Key}. Waiting for {WaitTime}ms.", key, waitTimeMs);
                        await Task.Delay((int)waitTimeMs, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to acquire distributed throttle permit for key {Key}. Failing open after retries.", key);
                    // Fail-open: if Redis is completely down, we don't want to block everything forever.
                    // The downstream Polly policy for the API will handle the resulting 429s.
                    return;
                }
            }
        }
    }
}