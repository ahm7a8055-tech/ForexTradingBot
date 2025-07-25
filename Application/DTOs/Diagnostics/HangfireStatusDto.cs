using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Diagnostics
{
    #region HangfireStatusDto
    /// <summary>
    /// Represents a snapshot of the overall status of the Hangfire background job processing system.
    /// This DTO aggregates statistics from all jobs, servers, and queues.
    /// </summary>
    public class HangfireStatusDto
    {
        #region Properties

        #region Job State Counts
        /// <summary>
        /// Gets or sets the number of jobs waiting in a queue to be processed.
        /// </summary>
        /// <example>15</example>
        public long EnqueuedCount { get; set; }

        /// <summary>
        /// Gets or sets the number of jobs scheduled to run at a future time.
        /// </summary>
        /// <example>5</example>
        public long ScheduledCount { get; set; }

        /// <summary>
        /// Gets or sets the number of jobs currently being processed by a worker.
        /// </summary>
        /// <example>2</example>
        public long ProcessingCount { get; set; }

        /// <summary>
        /// Gets or sets the number of jobs that have completed successfully.
        /// </summary>
        /// <example>1024</example>
        public long SucceededCount { get; set; }

        /// <summary>
        /// Gets or sets the number of jobs that have failed and are awaiting retry or manual intervention.
        /// </summary>
        /// <example>3</example>
        public long FailedCount { get; set; }

        /// <summary>
        /// Gets or sets the number of jobs that have been manually or automatically deleted.
        /// </summary>
        /// <example>50</example>
        public long DeletedCount { get; set; }
        #endregion

        #region Server Information
        /// <summary>
        /// Gets or sets the number of active Hangfire server instances (workers) processing jobs.
        /// </summary>
        /// <example>1</example>
        public int ServerCount { get; set; }

        /// <summary>
        /// Gets or sets a list of the names of the active Hangfire server instances.
        /// A server is a process that fetches and executes jobs.
        /// </summary>
        /// <example>["MYSERVER:12345"]</example>
        public List<string> Servers { get; set; } = new();
        #endregion

        #region Detailed Components
        /// <summary>
        /// Gets or sets a list of all known job queues and their current status.
        /// </summary>
        public List<HangfireQueueDto> Queues { get; set; } = new();

        /// <summary>
        /// Gets or sets a list of all configured recurring jobs and their status.
        /// </summary>
        public List<HangfireRecurringJobDto> RecurringJobs { get; set; } = new();
        #endregion

        #endregion
    }
    #endregion

    #region HangfireQueueDto
    /// <summary>
    /// Represents the status of a single Hangfire job queue.
    /// </summary>
    public class HangfireQueueDto
    {
        #region Properties

        #region Queue Details
        /// <summary>
        /// Gets or sets the name of the queue.
        /// </summary>
        /// <example>default</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of jobs currently waiting in this queue.
        /// </summary>
        /// <example>15</example>
        public long Length { get; set; }

        /// <summary>
        /// Gets or sets the number of jobs that have been fetched by a worker but are not yet completed.
        /// This is often 0 unless observed during a specific state.
        /// </summary>
        /// <example>0</example>
        public long Fetched { get; set; }

        // /// <summary>
        // /// Gets or sets the estimated time to process all jobs currently in the queue.
        // /// </summary>
        // /// <remarks>
        // /// This property is commented out because calculating it accurately is complex.
        // /// It would require historical data about average job processing times, which is beyond
        // /// the scope of standard Hangfire monitoring APIs.
        // /// </remarks>
        // public TimeSpan? EstimatedProcessingTime { get; set; }
        #endregion

        #endregion
    }
    #endregion

    #region HangfireRecurringJobDto
    /// <summary>
    /// Represents the detailed status of a single recurring job within Hangfire.
    /// </summary>
    public class HangfireRecurringJobDto
    {
        #region Properties

        #region Job Definition
        /// <summary>
        /// Gets or sets the unique identifier for the recurring job.
        /// </summary>
        /// <example>daily-cleanup</example>
        [Required]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CRON expression that defines the job's schedule.
        /// </summary>
        /// <example>0 0 * * *</example>
        [Required]
        public string Cron { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the queue where this job will be enqueued for execution.
        /// </summary>
        /// <example>critical</example>
        public string? Queue { get; set; }

        /// <summary>
        /// Gets or sets the C# method that is executed by the job.
        /// </summary>
        /// <example>MyNamespace.Services.CleanupService.PerformDailyCleanup</example>
        [Required]
        public string Method { get; set; } = string.Empty;
        #endregion

        #region Job Status
        /// <summary>
        /// Gets or sets a value indicating whether the recurring job has been removed.
        /// </summary>
        public bool Removed { get; set; }

        /// <summary>
        /// Gets or sets the last error message if the previous execution failed.
        /// This will be null if the last run was successful.
        /// </summary>
        /// <example>System.TimeoutException: The operation has timed out.</example>
        public string? Error { get; set; }
        #endregion

        #region Execution Timestamps
        /// <summary>
        /// Gets or sets a string representation of the next scheduled execution time.
        /// </summary>
        /// <remarks>
        /// This is a string for simplicity, as it's often retrieved from Hangfire's storage in this format.
        /// The consumer may need to parse it into a DateTimeOffset for calculations.
        /// </remarks>
        /// <example>2024-05-23T00:00:00.0000000Z</example>
        public string? NextExecution { get; set; }

        /// <summary>
        /// Gets or sets a string representation of the last actual execution time.
        /// </summary>
        /// <example>2024-05-22T00:00:01.1234567Z</example>
        public string? LastExecution { get; set; }

        /// <summary>
        /// Gets or sets a string representation of when the recurring job was first created.
        /// </summary>
        /// <example>2023-01-01T10:00:00.0000000Z</example>
        public string? CreatedAt { get; set; }
        #endregion

        #endregion
    }
    #endregion
}