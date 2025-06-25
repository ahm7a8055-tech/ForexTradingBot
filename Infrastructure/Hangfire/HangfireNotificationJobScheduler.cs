// File: Infrastructure/Hangfire/HangfireNotificationJobScheduler.cs
// Ensure the namespace matches your project structure for the Infrastructure layer.

using Application.Common.Interfaces; // CORRECT: References the interface from the Application layer
using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

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

        // Constructor to inject dependencies
        public HangfireNotificationJobScheduler(IBackgroundJobClient backgroundJobClient, ILogger<HangfireNotificationJobScheduler> logger)
        {
            _backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // --- Implementations for Synchronous Jobs ---

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