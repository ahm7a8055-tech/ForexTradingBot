// File: Infrastructure/Services/RedisDistributedThrottler.cs
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Polly;
using Polly.Retry;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class RedisDistributedThrottler : IDistributedThrottler
    {
        // ... (rest of the class is correct, including LuaScript, constructor, etc.) ...
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisDistributedThrottler> _logger;
        private readonly AsyncRetryPolicy _redisRetryPolicy;
        private static readonly Lazy<LuaScript> _throttleScript = new Lazy<LuaScript>(() => LuaScript.Prepare(ThrottleLuaScript));

        private const string ThrottleLuaScript =
            // ... (Your Lua script remains exactly the same) ...
            @"-- KEYS[1]: The unique key for the rate limit sorted set (e.g., 'throttle:telegram-api')
              -- ARGV[1]: The current unix timestamp in milliseconds
              -- ARGV[2]: The time window for the limit in milliseconds
              -- ARGV[3]: The maximum number of requests allowed in the window
              -- ARGV[4]: A unique identifier for the current request (e.g., a random number) to avoid collisions

              local key = KEYS[1]
              local now = tonumber(ARGV[1])
              local window = tonumber(ARGV[2])
              local limit = tonumber(ARGV[3])
              local member = ARGV[4]

              if not now or not window or not limit then
                -- Basic validation to prevent nil arithmetic
                return 500
              end

              local clear_before = now - window
              redis.call('ZREMRANGEBYSCORE', key, '-inf', clear_before)

              local current_count = redis.call('ZCARD', key)

              if current_count < limit then
                  redis.call('ZADD', key, now, member)
                  redis.call('EXPIRE', key, math.ceil(window / 1000) + 1)
                  return 0
              else
                  local oldest_entry = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
                  if oldest_entry and #oldest_entry >= 2 then
                      local oldest_timestamp = tonumber(oldest_entry[2])
                      return (oldest_timestamp + window) - now
                  end
                  return window / limit
              end";

        // ... (Constructor is correct) ...
        public RedisDistributedThrottler(IConnectionMultiplexer redis, ILogger<RedisDistributedThrottler> logger)
        {
            _redis = redis;
            _logger = logger;

            _redisRetryPolicy = Policy
                .Handle<RedisException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt)),
                    onRetry: (ex, ts, count, ctx) =>
                    {
                        _logger.LogWarning(ex, "Redis error in throttler for key '{ThrottleKey}'. Attempt {Count}. Retrying in {WaitTime}...",
                            ctx.TryGetValue("ThrottleKey", out var key) ? key.ToString() : "N/A",
                            count, ts);
                    });
        }


        public async Task WaitAsync(string throttleKey, int limit, TimeSpan window, CancellationToken cancellationToken)
        {
            IDatabase db = _redis.GetDatabase();
            var key = $"throttle:{throttleKey}";

            var pollyContext = new Context($"ThrottleWait_{throttleKey}")
            {
                ["ThrottleKey"] = throttleKey
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var waitTimeMs = await _redisRetryPolicy.ExecuteAsync<long>(async (ctx) =>
                    {
                        // Get the pre-loaded Lua script.
                        var loadedScript = _throttleScript.Value;
                        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        var uniqueMember = Random.Shared.NextInt64();

                        // <<< THE FIX IS HERE >>>
                        // When using a LuaScript object, you call its EvaluateAsync method,
                        // passing the database and the parameters object.
                        var result = await loadedScript.EvaluateAsync(db, new
                        {
                            key = (RedisKey)key,
                            now,
                            window = window.TotalMilliseconds,
                            limit,
                            member = uniqueMember
                        }).ConfigureAwait(false);

                        return (long)result;

                    }, pollyContext).ConfigureAwait(false);

                    if (waitTimeMs <= 0)
                    {
                        return; // Permit acquired
                    }

                    if (waitTimeMs > 0)
                    {
                        _logger.LogTrace("Global throttle limit reached for key '{Key}'. Waiting for {WaitTime}ms.", key, waitTimeMs);
                        await Task.Delay((int)waitTimeMs, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // Propagate cancellation
                }
                catch (Exception ex)
                {
                    // Re-throw the exception to allow caller's retry policy to handle it.
                    _logger.LogError(ex, "Failed to acquire distributed throttle permit for key '{Key}' after all retries. Propagating the error to the caller.", key);
                    throw;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}