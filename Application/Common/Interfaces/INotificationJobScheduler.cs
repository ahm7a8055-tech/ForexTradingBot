// File: Application/Common/Interfaces/INotificationJobScheduler.cs
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    /// <summary>
    /// Defines the contract for scheduling and enqueuing background jobs.
    /// </summary>
    public interface INotificationJobScheduler
    {
        /// <summary>
        /// Enqueues a synchronous job to be executed by Hangfire.
        /// </summary>
        /// <typeparam name="T">The type of the service that the job will call.</typeparam>
        /// <param name="methodCall">The expression representing the method call.</param>
        /// <returns>The ID of the created Hangfire job.</returns>
        string Enqueue<T>(Expression<Action<T>> methodCall) where T : class;

        /// <summary>
        /// Enqueues an asynchronous job to be executed by Hangfire.
        /// </summary>
        /// <typeparam name="T">The type of the service that the job will call.</typeparam>
        /// <param name="methodCall">The expression representing the method call.</param>
        /// <returns>The ID of the created Hangfire job.</returns>
        string Enqueue<T>(Expression<Func<T, Task>> methodCall) where T : class;

        /// <summary>
        /// Schedules a synchronous job to be executed after a specified delay.
        /// </summary>
        /// <typeparam name="T">The type of the service that the job will call.</typeparam>
        /// <param name="methodCall">The expression representing the method call.</param>
        /// <param name="delay">The delay before the job is to be executed.</param>
        /// <returns>The ID of the scheduled Hangfire job.</returns>
        string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay) where T : class;

        /// <summary>
        /// Schedules an asynchronous job to be executed after a specified delay.
        /// </summary>
        /// <typeparam name="T">The type of the service that the job will call.</typeparam>
        /// <param name="methodCall">The expression representing the method call.</param>
        /// <param name="delay">The delay before the job is to be executed.</param>
        /// <returns>The ID of the scheduled Hangfire job.</returns>
        string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay) where T : class;
    }
}