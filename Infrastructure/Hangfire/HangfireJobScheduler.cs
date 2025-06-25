// File: src/Infrastructure/Services/HangfireJobScheduler.cs (or wherever your scheduler resides)
// Ensure namespace matches your project structure.

using Application.Common.Interfaces; // For INotificationJobScheduler
using Hangfire; // For IBackgroundJobClient and BackgroundJob
using Microsoft.Extensions.Logging; // For ILogger
using StackExchange.Redis;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks; // Required for Task

namespace Infrastructure.Hangfire // Use the correct namespace for your Infrastructure layer
{
    /// <summary>
    /// Implements INotificationJobScheduler using Hangfire for background job processing.
    /// This class correctly interfaces with Hangfire's IBackgroundJobClient.
    /// </summary>
    public class HangfireJobScheduler : INotificationJobScheduler
    {
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ILogger<HangfireJobScheduler> _logger;
        private readonly IDatabase _redisDb;
        // Constructor to inject dependencies
        public HangfireJobScheduler(IConnectionMultiplexer redisConnection,IBackgroundJobClient backgroundJobClient, ILogger<HangfireJobScheduler> logger)
        {
            _redisDb = redisConnection.GetDatabase();
            _backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
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
                await _redisDb.KeyDeleteAsync(lockKey);
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
                // Call the actual Hangfire Enqueue method. It returns the JobId.
                string jobId = _backgroundJobClient.Enqueue(methodCall);
                _logger.LogDebug("Enqueued synchronous job for type {JobType}. Job ID: {JobId}", typeof(T).Name, jobId);
                return jobId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue synchronous job for type {JobType}.", typeof(T).Name);
                throw; // Re-throw to allow Hangfire's retry mechanisms or calling code to handle it.
            }
        }

        /// <summary>
        /// Enqueues an asynchronous job to Hangfire.
        /// </summary>
        public string Enqueue<T>(Expression<Func<T, Task>> methodCall) where T : class
        {
            try
            {
                // Call the actual Hangfire Enqueue method for async jobs.
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
        /// Schedules a synchronous job to be executed after a specified delay.
        /// </summary>
        public string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay) where T : class
        {
            try
            {
                // Call the actual Hangfire Schedule method for synchronous jobs.
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

        /// <summary>
        /// Schedules an asynchronous job to be executed after a specified delay.
        /// </summary>
        public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay) where T : class
        {
            try
            {
                // Call the actual Hangfire Schedule method for asynchronous jobs.
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

        // Removed any explicit interface implementations that were incorrect or redundant.
        // The public methods now correctly implement the interface contract.
    }
}