namespace TelegramPanel.Queue.Models
{
    public class UpdateQueueOptions
    {
        public int MaxConcurrency { get; set; } = 10;
        public int QueueCapacity { get; set; } = 1000;
        public int SupervisorLoopIntervalMs { get; set; } = 10000;
        public int MetricsReportIntervalSeconds { get; set; } = 30;
        public string DeadLetterQueueName { get; set; } = "telegram:updates:deadletter";
        public int ShutdownTimeoutSeconds { get; set; } = 30;

        // SECURE-FIX: Added configuration for producer resilience
        public int ProducerRedisTimeoutSeconds { get; set; } = 25; // How long to wait for a single message
        public int ProducerMaxRetryAttempts { get; set; } = 5;      // How many times to retry on connection errors
        public int ProducerCircuitBreakerFailures { get; set; } = 5; // How many failures before opening the circuit
        public int ProducerCircuitBreakSeconds { get; set; } = 60;  // How long the circuit stays open
    }
}