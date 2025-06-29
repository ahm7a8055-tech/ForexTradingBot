namespace TelegramPanel.Queue.Models
{
    public class UpdateQueueOptions
    {
        // --- EXISTING PROPERTIES ---
        public int MaxConcurrency { get; set; } = 10;
        public int QueueCapacity { get; set; } = 50000;
        public int ProducerRedisTimeoutSeconds { get; set; } = 10;
        public int ProducerCircuitBreakerFailures { get; set; } = 5;
        public int ProducerCircuitBreakSeconds { get; set; } = 30;
        public int ProducerMaxRetryAttempts { get; set; } = 5;
        public int SupervisorLoopIntervalMs { get; set; } = 5000;
        public int MetricsReportIntervalSeconds { get; set; } = 30;
        public int ShutdownTimeoutSeconds { get; set; } = 15;

        // --- NEW PROPERTIES FOR RELIABLE QUEUE ---
        public string QueueName { get; set; } = "queue:telegram:updates";
        public string DeadLetterQueueName { get; set; } = "queue:telegram:updates:deadletter";
    }
}