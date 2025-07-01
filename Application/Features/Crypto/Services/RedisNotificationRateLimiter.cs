// File: Infrastructure/Services/RedisNotificationRateLimiter.cs

using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using StackExchange.Redis;

public class RedisNotificationRateLimiter : INotificationRateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisNotificationRateLimiter> _logger;
    private readonly AsyncRetryPolicy _redisRetryPolicy;

    #region Lua Scripts
    // (Lua scripts remain the same - they are already quite robust for their intended purpose)
    private const string DecrementRateLimitLuaScript =
     @"-- KEYS[1]: The unique key for the user's rate limit sorted set
          return redis.call('ZREMRANGEBYRANK', KEYS[1], -1, -1)";

    private const string RateLimiterLuaScript =
        @"-- KEYS[1]: The unique key for the user's rate limit sorted set
          -- ARGV[1]: The current unix timestamp (in milliseconds)
          -- ARGV[2]: The time window for the limit (in milliseconds)
          -- ARGV[3]: The maximum number of requests allowed in the window
          local clear_before = tonumber(ARGV[1]) - tonumber(ARGV[2])
          redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', clear_before)
          local current_count = redis.call('ZCARD', KEYS[1])
          if current_count >= tonumber(ARGV[3]) then
              return current_count
          end
          local new_member = ARGV[1]
          redis.call('ZADD', KEYS[1], new_member, new_member)
          redis.call('EXPIRE', KEYS[1], math.ceil(tonumber(ARGV[2]) / 1000) + 60)
          return current_count + 1";

    private const string CheckRateLimitLuaScript =
        @"-- KEYS[1]: The unique key for the user's rate limit sorted set
          -- ARGV[1]: The current unix timestamp (in milliseconds)
          -- ARGV[2]: The time window for the limit (in milliseconds)
          local clear_before = tonumber(ARGV[1]) - tonumber(ARGV[2])
          redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', clear_before)
          local current_count = redis.call('ZCARD', KEYS[1])
          return current_count";

    private const string IncrementRateLimitLuaScript =
        @"-- KEYS[1]: The unique key for the user's rate limit sorted set
          -- ARGV[1]: The current unix timestamp (in milliseconds)
          -- ARGV[2]: The time window for the limit (in milliseconds)
          local new_member = ARGV[1]
          redis.call('ZADD', KEYS[1], new_member, new_member)
          redis.call('EXPIRE', KEYS[1], math.ceil(tonumber(ARGV[2]) / 1000) + 60)
          return 1";
    #endregion

    public RedisNotificationRateLimiter(IConnectionMultiplexer redis, ILogger<RedisNotificationRateLimiter> logger)
    {
        _redis = redis;
        _logger = logger;

        // More conservative retry for critical operations like rate limiting.
        _redisRetryPolicy = Policy
            .Handle<RedisConnectionException>()
            .Or<RedisTimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3, // Increased to 3 retries
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, retryAttempt - 1)), // Exponential backoff (200ms, 400ms, 800ms)
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "REDIS RETRY (RateLimiter): Transient error. Retrying in {TimeSpan}s. Attempt {RetryCount}/3. Context: {Context}",
                                       timeSpan.TotalSeconds, retryCount, context.OperationKey);
                });
    }

    private record RateLimitParams(string Key, long Now, long Window, int Limit);

    // This method is for "check-only" and is fine to be fail-open if needed.
    // It does not modify state, so a fail-open is less risky for core rate limit integrity.
    public async Task<bool> IsUserAtOrOverLimitAsync(long telegramUserId, int limit, TimeSpan period)
    {
        IDatabase db = _redis.GetDatabase();
        string key = $"notif_limit:v2:{telegramUserId}";
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long window = (long)period.TotalMilliseconds;

        try
        {
            Context context = new($"IsUserAtOrOverLimitAsync_User_{telegramUserId}");
            RedisResult result = await _redisRetryPolicy.ExecuteAsync(async (ctx) =>
            {
                _logger.LogTrace("Attempting Redis 'CheckRateLimitLuaScript' for User {UserId}. Context: {Context}", telegramUserId, ctx.OperationKey);
                return await db.ScriptEvaluateAsync(
                    CheckRateLimitLuaScript,
                    new RedisKey[] { key },
                    new RedisValue[] { now, window }
                );
            }, context);

            long currentCount = (long)result;
            bool isAtOrOverLimit = currentCount >= limit;

            if (isAtOrOverLimit)
            {
                _logger.LogInformation("Rate limit CHECK for User {UserId}: AT or OVER limit. Count: {CurrentCount}/{Limit}.", telegramUserId, currentCount, limit);
            }
            else
            {
                _logger.LogTrace("Rate limit CHECK for User {UserId}: UNDER limit. Count: {CurrentCount}/{Limit}.", telegramUserId, currentCount, limit);
            }
            return isAtOrOverLimit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REDIS FAILURE (IsUserAtOrOverLimitAsync): Could not contact Redis for rate limit check for User {UserId}. Defaulting to NOT over limit (fail-open).", telegramUserId);
            return false; // Fail-open for the check-only method is usually acceptable.
        }
    }

    // This method increments state and is fine to be "best-effort."
    // If it fails, the user might get an extra notification later, but the system doesn't block.
    public async Task IncrementUsageAsync(long telegramUserId, TimeSpan period)
    {
        IDatabase db = _redis.GetDatabase();
        string key = $"notif_limit:v2:{telegramUserId}";
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long window = (long)period.TotalMilliseconds;

        try
        {
            Context context = new($"IncrementUsageAsync_User_{telegramUserId}");
            await _redisRetryPolicy.ExecuteAsync(async (ctx) =>
            {
                _logger.LogTrace("Attempting Redis 'IncrementRateLimitLuaScript' for User {UserId}. Context: {Context}", telegramUserId, ctx.OperationKey);
                _ = await db.ScriptEvaluateAsync(
                    IncrementRateLimitLuaScript,
                    new RedisKey[] { key },
                    new RedisValue[] { now, window }
                );
            }, context);
            _logger.LogInformation("Successfully incremented rate limit usage for User {UserId}.", telegramUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REDIS FAILURE (IncrementUsageAsync): Failed to increment rate limit usage for User {UserId} after retries. This may affect quota accuracy.", telegramUserId);
            // Do not throw; this is best-effort.
        }
    }

    /// <summary>
    /// **SHIELDED ATOMIC CHECK AND INCREMENT**
    /// Asynchronously checks if a specific user has exceeded a predefined notification rate limit and increments the count if they have not.
    /// This method performs both actions atomically.
    /// **This method now defaults to FAIL-CLOSED if Redis is unavailable after retries.**
    /// </summary>
    public async Task<bool> IsUserOverLimitAsync(long telegramUserId, int limit, TimeSpan period)
    {
        IDatabase db = _redis.GetDatabase();
        RateLimitParams parameters = new(
            Key: $"notif_limit:v2:{telegramUserId}",
            Now: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Window: (long)period.TotalMilliseconds,
            Limit: limit
        );

        try
        {
            Context context = new($"IsUserOverLimitAsync_User_{telegramUserId}");
            RedisResult result = await _redisRetryPolicy.ExecuteAsync(async (ctx) =>
            {
                _logger.LogTrace("Attempting Redis 'RateLimiterLuaScript' (atomic check-and-increment) for User {UserId}. Context: {Context}", telegramUserId, ctx.OperationKey);
                return await db.ScriptEvaluateAsync(
                    RateLimiterLuaScript,
                    new RedisKey[] { parameters.Key },
                    new RedisValue[] { parameters.Now, parameters.Window, parameters.Limit }
                );
            }, context);

            long returnedCount = (long)result;
            bool isOverLimit = returnedCount > limit; // Correct logic from previous fix

            if (isOverLimit)
            {
                // The Lua script already attempted an increment that pushed it over the limit.
                // We must roll back this erroneous increment.
                _logger.LogWarning("Rate limit VIOLATION for User {UserId}. Count would be {ReturnedCount}/{Limit}. Rolling back immediately.",
                    telegramUserId, returnedCount, limit);
                // Call DecrementUsageAsync which also has its own retry policy.
                await DecrementUsageAsync(telegramUserId);
            }
            else
            {
                _logger.LogInformation("Rate limit check PASSED for User {UserId}. Slot reserved. New count: {ReturnedCount}/{Limit}.",
                    telegramUserId, returnedCount, limit);
            }
            return isOverLimit;
        }
        catch (Exception ex)
        {
            // =========================================================================
            // == SHIELD ENHANCEMENT: FAIL-CLOSED on IsUserOverLimitAsync               ==
            // =========================================================================
            // If we cannot contact Redis for the critical atomic check-and-increment,
            // we assume the user IS over the limit to be safe and prevent over-sending.
            _logger.LogCritical(ex, "REDIS FAILURE (IsUserOverLimitAsync): Could not contact Redis for atomic rate limit check for User {UserId} after retries. Defaulting to OVER LIMIT (fail-closed).", telegramUserId);
            return true; // FAIL-CLOSED: Assume over limit if Redis is down for this critical operation.
        }
    }

    // DecrementUsageAsync also needs robust retry and error logging.
    public async Task DecrementUsageAsync(long telegramUserId)
    {
        IDatabase db = _redis.GetDatabase();
        string key = $"notif_limit:v2:{telegramUserId}";

        try
        {
            Context context = new($"DecrementUsageAsync_User_{telegramUserId}");
            await _redisRetryPolicy.ExecuteAsync(async (ctx) =>
            {
                _logger.LogTrace("Attempting Redis 'DecrementRateLimitLuaScript' for User {UserId}. Context: {Context}", telegramUserId, ctx.OperationKey);
                RedisResult result = await db.ScriptEvaluateAsync(
                    DecrementRateLimitLuaScript,
                    new RedisKey[] { key }
                );
                // Lua script returns the number of elements removed (0 or 1).
                if ((long)result == 1)
                {
                    _logger.LogWarning("Successfully rolled back (decremented) rate limit usage for User {UserId}.", telegramUserId);
                }
                else
                {
                    _logger.LogWarning("Attempted to roll back rate limit for User {UserId}, but no entry was found to remove (or ZREMRANGEBYRANK failed to find the highest score). This is okay if the slot was never truly taken.", telegramUserId);
                }
            }, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REDIS FAILURE (DecrementUsageAsync): Failed to DECREMENT rate limit usage for User {UserId} after retries. Their quota may be temporarily inaccurate.", telegramUserId);
            // Do not throw; this is best-effort but critical to log.
        }
    }
}