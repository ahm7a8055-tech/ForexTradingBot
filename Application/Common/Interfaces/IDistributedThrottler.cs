namespace Application.Common.Interfaces
{
    /// <summary>
    /// Defines a contract for a distributed throttler, designed to limit the rate of operations
    /// across multiple servers and processes. This is essential for respecting global API rate limits.
    /// </summary>
    public interface IDistributedThrottler
    {
        /// <summary>
        /// Asynchronously waits until a permit is available to proceed. This method blocks until
        /// the rate limit window allows for another operation.
        /// </summary>
        /// <param name="throttleKey">A unique key identifying the resource to be throttled (e.g., "telegram-api-global").</param>
        /// <param name="limit">The number of permits allowed within the time window.</param>
        /// <param name="window">The duration of the time window.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        Task WaitAsync(string throttleKey, int limit, TimeSpan window, CancellationToken cancellationToken);
    }
}