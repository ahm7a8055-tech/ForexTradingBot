// The namespace must match the folder structure for clarity.
namespace TelegramPanel.Queue.Models
{
    public class UpdateQueueOptions
    {
        public int MaxConcurrency { get; set; } = 100;
        public int QueueCapacity { get; set; } = 10000;
        public string DeadLetterQueueName { get; set; } = "telegram:updates:deadletter";
        public int SupervisorLoopIntervalMs { get; set; } = 5000;
        public int ShutdownTimeoutSeconds { get; set; } = 30;

        // --- FIX: Add this missing property ---
        public int MetricsReportIntervalSeconds { get; set; } = 10;
    }
}