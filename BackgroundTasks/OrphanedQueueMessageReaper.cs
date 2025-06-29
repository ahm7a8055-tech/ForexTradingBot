// In a new file, e.g., BackgroundTasks/Services/OrphanedQueueMessageReaper.cs
using StackExchange.Redis;

public class OrphanedQueueMessageReaper : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrphanedQueueMessageReaper> _logger;

    public OrphanedQueueMessageReaper(IServiceProvider sp, ILogger<OrphanedQueueMessageReaper> logger)
    {
        _serviceProvider = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Wait for a set interval, e.g., 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            _logger.LogInformation("Running orphaned message reaper job...");

            await using var scope = _serviceProvider.CreateAsyncScope();
            var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var server = redis.GetServer(redis.GetEndPoints().First());
            var db = redis.GetDatabase();

            // Find all processing queues
            var processingQueues = server.Keys(pattern: "*:processing:*");

            foreach (var queueKey in processingQueues)
            {
                // This is a simplified check. A more robust solution would store timestamps.
                // For now, we just requeue anything we find.
                while (await db.ListRightPopLeftPushAsync(queueKey, "queue:telegram:updates") != RedisValue.Null)
                {
                    _logger.LogWarning("Reaped orphaned message from processing queue '{Queue}' and moved it back to main queue.", queueKey);
                }
            }
        }
    }
}