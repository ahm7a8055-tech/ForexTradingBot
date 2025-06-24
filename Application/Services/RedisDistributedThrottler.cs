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
    /// <summary>
    /// Implements a distributed throttler using Redis and a sliding window log algorithm via a Lua script.
    /// This ensures that operations across multiple distributed workers can be limited to a global rate (e.g., 30 calls per second).
    /// </summary>
    public class RedisDistributedThrottler : IDistributedThrottler
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisDistributedThrottler> _logger;
        private readonly AsyncRetryPolicy _redisRetryPolicy;

        // <<< PERF CHANGE 1: Use LuaScript.Prepare for script caching. >>>
        // This ensures the script is loaded only once and referenced by its hash, reducing network traffic and server parsing.
        private static readonly Lazy<LuaScript> _throttleScript = new Lazy<LuaScript>(() => LuaScript.Prepare(ThrottleLuaScript));

        // Lua script for a sliding window log. It cleans old entries, checks the count, and only adds a new entry if under the limit.
        // It returns the time to wait in milliseconds if the limit is exceeded.
        private const string ThrottleLuaScript =
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

              local clear_before = now - window
              -- Remove all timestamps that are outside the current window
              redis.call('ZREMRANGEBYSCORE', key, '-inf', clear_before)

              -- Get the current number of requests in the window
              local current_count = redis.call('ZCARD', key)

              if current_count < limit then
                  -- If under the limit, add the new request and set expiration
                  redis.call('ZADD', key, now, member)
                  -- Set expiration to the window size plus a small buffer to avoid race conditions.
                  -- Note: EXPIRE on a key with a sorted set doesn't expire members individually,
                  -- but it ensures the key itself is removed after a period, cleaning up old data.
                  -- The window duration is used here. Adding +1 for safety.
                  redis.call('EXPIRE', key, math.ceil(window / 1000) + 1)
                  return 0 -- Success, no wait time
              else
                  -- If at or over the limit, find the oldest timestamp to calculate wait time
                  local oldest_entry = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
                  if oldest_entry and #oldest_entry >= 2 then
                      local oldest_timestamp = tonumber(oldest_entry[2])
                      -- Wait time is the difference between when the oldest request expires and now.
                      -- oldest_timestamp + window is the expiration time of the oldest request.
                      return (oldest_timestamp + window) - now
                  end
                  -- Fallback wait time if something goes wrong (e.g., unexpected Redis state).
                  -- This calculation is less precise but provides a reasonable default.
                  return window / limit
              end";

        public RedisDistributedThrottler(IConnectionMultiplexer redis, ILogger<RedisDistributedThrottler> logger)
        {
            _redis = redis;
            _logger = logger;

            // Configure Polly policy for retrying Redis operations.
            _redisRetryPolicy = Policy
                .Handle<RedisException>() // Handle specific Redis errors
                .WaitAndRetryAsync(
                    retryCount: 3, // Number of retries
                                   // Use exponential backoff for wait times (e.g., 100ms, 200ms, 400ms)
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt)),
                    // Log a warning before each retry
                    onRetry: (ex, ts, count, ctx) =>
                    {
                        // Use the context to include the throttler key in the log message.
                        _logger.LogWarning(ex, "Redis error in throttler for key '{ThrottleKey}'. Attempt {Count} failed after waiting {WaitTime}. Retrying...",
                            ctx.TryGetValue("ThrottleKey", out var key) ? key.ToString() : "N/A",
                            count, ts);
                    });
        }

        /// <summary>
        /// Waits until a permit is acquired from the distributed throttler.
        /// </summary>
        /// <param name="throttleKey">A unique identifier for the rate limit (e.g., "telegram-api").</param>
        /// <param name="limit">The maximum number of requests allowed within the window.</param>
        /// <param name="window">The time window for the limit.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task that completes when a permit is acquired.</returns>
        public async Task WaitAsync(string throttleKey, int limit, TimeSpan window, CancellationToken cancellationToken)
        {
            IDatabase db = _redis.GetDatabase();
            var key = $"throttle:{throttleKey}";

            // Create a Polly context for richer logging within the retry policy.
            var pollyContext = new Context($"ThrottleWait_{throttleKey}")
            {
                ["ThrottleKey"] = throttleKey // Pass the key to the context for use in onRetry delegate.
            };

            // Loop until a permit is acquired or cancellation is requested.
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Execute the Redis Lua script evaluation within the retry policy.
                    var waitTimeMs = await _redisRetryPolicy.ExecuteAsync<long>(async (ctx) =>
                    {
                        // Get the pre-loaded Lua script.
                        var loadedScript = _throttleScript.Value;
                        // Get current timestamp in milliseconds.
                        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        // <<< PERF CHANGE 2: Use Random.Shared.NextInt64() for a lightweight, non-allocating unique member ID. >>>
                        // Guid.NewGuid().ToString() causes unnecessary allocations and is slower.
                        var uniqueMember = Random.Shared.NextInt64();

                        // <<< FIX: Use the correct ScriptEvaluateAsync overload for LuaScript objects. >>>
                        // This overload takes a single anonymous object for parameters.
                        // StackExchange.Redis automatically maps RedisKey types to KEYS[] and other values to ARGV[].
                        var result = await db.ScriptEvaluateAsync(loadedScript, new
                        {
                            key = (RedisKey)key, // Maps to KEYS[1]
                            now,                 // Maps to ARGV[1]
                            window = (double)window.TotalMilliseconds, // Maps to ARGV[2]
                            limit,               // Maps to ARGV[3]
                            member = (RedisValue)uniqueMember // Maps to ARGV[4]
                        }).ConfigureAwait(false); // Added ConfigureAwait(false) for the Redis I/O operation.

                        // The Lua script returns the wait time in milliseconds, or 0 if successful.
                        return (long)result;
                    }, pollyContext).ConfigureAwait(false); // Added ConfigureAwait(false) for the ExecuteAsync call itself.

                    // If waitTimeMs is 0 or less, a permit was acquired.
                    if (waitTimeMs <= 0)
                    {
                        return; // Exit the method, permit acquired.
                    }

                    // If waitTimeMs is positive, the limit was reached, and we need to wait.
                    if (waitTimeMs > 0)
                    {
                        _logger.LogTrace("Global throttle limit reached for key '{Key}'. Waiting for {WaitTime}ms.", key, waitTimeMs);
                        // <<< FIX: Added .ConfigureAwait(false) for best async practice >>>
                        // Prevents potential deadlocks by not trying to resume on the original synchronization context.
                        await Task.Delay((int)waitTimeMs, cancellationToken).ConfigureAwait(false);
                    }
                }
                // <<< FIX: Explicitly catch OperationCanceledException. >>>
                // This ensures that if cancellation occurs during Task.Delay or other awaited operations,
                // it's handled correctly and not logged as an unexpected error.
                catch (OperationCanceledException)
                {
                    // Cancellation was requested. Propagate the exception.
                    throw; // Re-throwing ensures the cancellation propagates up the call stack.
                }
                // Generic exception catch for unexpected errors during Redis operations.
                catch (Exception ex)
                {
                    // Implement the "fail-open" strategy: If Redis is unavailable or other errors occur,
                    // don't block the application. Log the error and allow the operation to proceed.
                    // The downstream system (e.g., the API caller) will have to handle the consequences (e.g., HTTP 429).
                    _logger.LogError(ex, "Failed to acquire distributed throttle permit for key '{Key}' after retries. Failing open.", key);
                    return; // Exit the loop and method, effectively allowing the operation to proceed.
                }
            }

            // If the loop terminates due to cancellation, ensure the exception is thrown.
            // This is a safeguard, as Task.Delay should have thrown OperationCanceledException.
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}