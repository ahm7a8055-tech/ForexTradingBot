// File: Infrastructure/Hangfire/HangfireNotificationJobScheduler.cs
// Ensure the namespace matches your project structure for the Infrastructure layer.

using Application.Common.Interfaces; // CORRECT: References the interface from the Application layer
using Hangfire;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Linq.Expressions;

namespace Infrastructure.Hangfire // CORRECT: This is where your implementation class lives
{
    /// <summary>
    /// Implements INotificationJobScheduler using Hangfire for background job processing.
    /// This class correctly interfaces with Hangfire's IBackgroundJobClient.
    /// </summary>
    public class HangfireNotificationJobScheduler : INotificationJobScheduler
    {
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ILogger<HangfireNotificationJobScheduler> _logger;
        private readonly IDatabase _redisDb;
        // Constructor to inject dependencies
        public HangfireNotificationJobScheduler(IConnectionMultiplexer redisConnection, IBackgroundJobClient backgroundJobClient, ILogger<HangfireNotificationJobScheduler> logger)
        {
            _redisDb = redisConnection.GetDatabase();
            _backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        // --- Implementations for Synchronous Jobs ---


        public async Task<bool> TryAcquireLockAsync(string lockKey, TimeSpan expiry)
        {
            try
            {
                return await _redisDb.StringSetAsync(lockKey, "locked", expiry, When.NotExists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire lock '{LockKey}'.", lockKey);
                // Fail open or fail closed depends on your strategy.
                // Failing open means a new dispatch might start concurrently if lock fails.
                // Failing closed would mean no new dispatches start if lock fails.
                // For now, let's assume a lock failure is a reason to skip if critical, but we'll rely on other handlers for DispatchNewsNotificationAsync.
                // A safer default is to return false, so Orchestrator skips.
                return false;
            }
        }

        public async Task ReleaseLockAsync(string lockKey)
        {
            try
            {
                _ = await _redisDb.KeyDeleteAsync(lockKey);
                _logger.LogDebug("Released lock '{LockKey}'.", lockKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release lock '{LockKey}'.", lockKey);
            }
        }


        /// <summary>
        /// Enqueues a synchronous job to Hangfire.
        /// </summary>
        public string Enqueue<T>(Expression<Action<T>> methodCall) where T : class
        {
            try
            {
                string jobId = _backgroundJobClient.Enqueue(methodCall);
                _logger.LogDebug("Enqueued synchronous job for type {JobType}. Job ID: {JobId}", typeof(T).Name, jobId);
                return jobId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue synchronous job for type {JobType}.", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Schedules a synchronous job to be executed after a specified delay.
        /// </summary>
        public string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay) where T : class
        {
            try
            {
                string jobId = _backgroundJobClient.Schedule(methodCall, delay);
                _logger.LogDebug("Scheduled synchronous job for type {JobType} with delay {Delay}. Job ID: {JobId}",
                    typeof(T).Name, delay, jobId);
                return jobId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule synchronous job for type {JobType} with delay {Delay}.", typeof(T).Name, delay);
                throw;
            }
        }

        // --- Implementations for Asynchronous Jobs ---

        /// <summary>
        /// Enqueues an asynchronous job to Hangfire.
        /// </summary>
        public string Enqueue<T>(Expression<Func<T, Task>> methodCall) where T : class
        {
            try
            {
                string jobId = _backgroundJobClient.Enqueue(methodCall);
                _logger.LogDebug("Enqueued asynchronous job for type {JobType}. Job ID: {JobId}", typeof(T).Name, jobId);
                return jobId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue asynchronous job for type {JobType}.", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Schedules an asynchronous job to be executed after a specified delay.
        /// </summary>
        public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay) where T : class
        {
            try
            {
                string jobId = _backgroundJobClient.Schedule(methodCall, delay);
                _logger.LogDebug("Scheduled asynchronous job for type {JobType} with delay {Delay}. Job ID: {JobId}",
                    typeof(T).Name, delay, jobId);
                return jobId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule asynchronous job for type {JobType} with delay {Delay}.", typeof(T).Name, delay);
                throw;
            }
        }
    }
}