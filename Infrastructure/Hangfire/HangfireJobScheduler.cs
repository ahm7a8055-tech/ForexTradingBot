// File: src/Infrastructure/Services/HangfireJobScheduler.cs (or wherever your scheduler resides)
// Ensure namespace matches your project structure.

using Application.Common.Interfaces; // For INotificationJobScheduler
using Hangfire; // For IBackgroundJobClient and BackgroundJob
using Microsoft.Extensions.Logging; // For ILogger
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

        // Constructor to inject dependencies
        public HangfireJobScheduler(IBackgroundJobClient backgroundJobClient, ILogger<HangfireJobScheduler> logger)
        {
            _backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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