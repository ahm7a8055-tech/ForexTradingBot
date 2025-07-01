// The namespace must match the folder structure for clarity.
namespace TelegramPanel.Queue.Models.Interface
{
    public interface IQueueMetricsService
    {
        void IncrementProcessed();
        void IncrementFailed();
        void IncrementDeadLettered();
        void UpdateQueueDepth(long depth);
        void UpdateConcurrency(int current, int max);
        Task ReportMetricsAsync(CancellationToken stoppingToken);
    }
}