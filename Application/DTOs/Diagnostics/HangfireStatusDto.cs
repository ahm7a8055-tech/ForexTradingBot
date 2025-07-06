using System.Collections.Generic;

namespace Application.DTOs.Diagnostics
{
    public class HangfireStatusDto
    {
        public long EnqueuedCount { get; set; }
        public long ScheduledCount { get; set; }
        public long ProcessingCount { get; set; }
        public long SucceededCount { get; set; }
        public long FailedCount { get; set; }
        public long DeletedCount { get; set; }
        public int ServerCount { get; set; }
        public List<string> Servers { get; set; } = new List<string>();
        public List<HangfireQueueDto> Queues { get; set; } = new List<HangfireQueueDto>();
        public List<HangfireRecurringJobDto> RecurringJobs { get; set; } = new List<HangfireRecurringJobDto>();
    }

    public class HangfireQueueDto
    {
        public string Name { get; set; } = string.Empty;
        public long Length { get; set; }
        public long Fetched { get; set; }
        // public TimeSpan? EstimatedProcessingTime { get; set; } // More complex to calculate accurately
    }

    public class HangfireRecurringJobDto
    {
        public string Id { get; set; } = string.Empty;
        public string Cron { get; set; } = string.Empty;
        public string? Queue { get; set; }
        public string? NextExecution { get; set; } // Using string for simplicity, could be DateTimeOffset
        public string? LastExecution { get; set; } // Using string for simplicity
        public string? CreatedAt { get; set; }     // Using string for simplicity
        public bool Removed { get; set; }
        public string? Error { get; set; }
        public string Method { get; set; } = string.Empty;
    }
}
