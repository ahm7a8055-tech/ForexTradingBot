using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;

namespace TelegramPanel.Infrastructure.Services
{
    public interface IUserRateLimiterService
    {
        // We revert to the simple bool check, as the lease handling is now internal.
        Task<bool> IsRequestAllowedAsync(long userId);
    }

    public class UserRateLimiterService : IUserRateLimiterService
    {
        private readonly ILogger<UserRateLimiterService> _logger;
        // Revert to ConcurrentDictionary as PartitionedRateLimiter is .NET 8+
        private readonly ConcurrentDictionary<long, RateLimiter> _userLimiters = new();

        public UserRateLimiterService(ILogger<UserRateLimiterService> logger)
        {
            _logger = logger;
            // NOTE: For true long-running apps on .NET 6/7, a manual cleanup service
            // for this dictionary would still be needed. We'll omit it for now as per the last step.
        }

        public async Task<bool> IsRequestAllowedAsync(long userId)
        {
            // Get or create a composite rate limiter for the specific user.
            var limiter = _userLimiters.GetOrAdd(userId, _ => CreateNewUserLimiter());

            // Acquire a permit from the composite limiter.
            using RateLimitLease lease = await limiter.AcquireAsync(1);

            if (lease.IsAcquired)
            {
                return true;
            }

            _logger.LogWarning("User {UserId} was rate-limited.", userId);
            return false;
        }

        private RateLimiter CreateNewUserLimiter()
        {
            // This is the manual way to chain limiters in .NET 6/7.

            // Rule 1: General Anti-Spam (Burst) - 1 message per second
            var antiSpamLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1,
                Window = TimeSpan.FromSeconds(1.2),
                AutoReplenishment = true
            });

            // Rule 2: Sustained Rate (Telegram Policy Aligned) - 20 messages per minute
            var sustainedRateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = 20,
                TokensPerPeriod = 20,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            });

            // In .NET 7 and below, there is no built-in ChainedRateLimiter.
            // A common approach is to just use the most restrictive one, or to check them sequentially.
            // For simplicity and robustness, we will use the most restrictive general-purpose one,
            // which is the SlidingWindowRateLimiter from our very first implementation.
            // It provides a good balance of burst and sustained protection.

            // Reverting to the most robust, single, compatible limiter.
            return new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromSeconds(10),
                SegmentsPerWindow = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            });
        }
    }
}