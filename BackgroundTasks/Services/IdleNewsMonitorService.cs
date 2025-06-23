// File: BackgroundTasks/Services/IdleNewsMonitorService.cs

using Application.Interfaces;
using Polly;
using Polly.CircuitBreaker;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Logging; // Ensure this is present for ILogger
using Microsoft.Extensions.DependencyInjection; // Ensure this is present for IServiceProvider and IServiceScope
using System; // Ensure this is present for TimeSpan, Guid, etc.
using System.Threading; // Ensure this is present for CancellationToken
using System.Threading.Tasks; // Ensure this is present for Task


namespace BackgroundTasks.Services
{
    public class IdleNewsMonitorService : BackgroundService, IIdleNewsMonitorService
    {
        private readonly ILogger<IdleNewsMonitorService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDatabase _redisDb;
        private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy; // ✅ NEW
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(10); // Run less frequently
        private const string NewsQueueKey = "system_news_queue";

        // --- CONFIGURATION TO DISABLE THE SERVICE ---
        // Set this to 'false' to prevent the service from processing queue items.
        // The service will still start and log its initial message, but the loop will do nothing.
        private const bool IsServiceEnabled = false;
        // ------------------------------------------


        public IdleNewsMonitorService(
            ILogger<IdleNewsMonitorService> logger,
            IServiceProvider serviceProvider,
            IConnectionMultiplexer redisConnection)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _redisDb = redisConnection.GetDatabase();

            // ✅ NEW: Define a circuit breaker policy.
            // If 2 consecutive exceptions occur, the circuit will "break" for 5 minutes.
            _circuitBreakerPolicy = Policy
                .Handle<RedisException>() // Handle any Redis-related exception
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 2,
                    durationOfBreak: TimeSpan.FromMinutes(5),
                    onBreak: (exception, timespan) =>
                    {
                        _logger.LogCritical(exception, "IdleNewsMonitorService Circuit Breaker opened for {BreakDuration}. No queue processing will occur.", timespan);
                    },
                    onReset: () => _logger.LogInformation("IdleNewsMonitorService Circuit Breaker reset. Resuming normal operation."),
                    onHalfOpen: () => _logger.LogWarning("IdleNewsMonitorService Circuit Breaker is now half-open. The next operation will determine its state.")
                );
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Idle News Monitor Service (Queue Drainer) is starting.");

            // Check the enablement flag immediately upon starting
            if (!IsServiceEnabled)
            {
                _logger.LogWarning("Idle News Monitor Service is DISABLED. No queue processing will occur.");
                // We still want the service to register as active, but prevent the loop from running.
                // A simple way is to just keep the token from being cancelled, but not enter the loop.
                // Or, more cleanly, return gracefully after logging.
                await Task.Delay(Timeout.Infinite, stoppingToken); // Keep the service alive but inactive
                return;
            }

            // --- Original logic starts here, but is now guarded by IsServiceEnabled ---
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Initial delay

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // ✅ NEW: Execute all logic within the circuit breaker
                    await _circuitBreakerPolicy.ExecuteAsync(async () =>
                    {
                        // ✅ REVISED LOGIC: Simple, robust queue length check.
                        long queueLength = await _redisDb.ListLengthAsync(NewsQueueKey);
                        if (queueLength > 0)
                        {
                            _logger.LogInformation("Idle Monitor found {QueueLength} items in the system news queue. Processing one.", queueLength);
                            await ProcessNewsItemFromQueue(stoppingToken);
                        }
                        else
                        {
                            _logger.LogTrace("System news queue is empty. Nothing to process.");
                        }
                    });
                }
                catch (BrokenCircuitException)
                {
                    // This exception is thrown when we try to execute while the circuit is open.
                    // We log it at a lower level because the onBreak delegate already logged the critical error.
                    _logger.LogWarning("Skipping idle check because the Redis circuit is open.");
                }
                catch (Exception ex)
                {
                    // Catch any other unexpected errors.
                    _logger.LogError(ex, "An unhandled error occurred in the Idle News Monitor service loop.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }

        private async Task ProcessNewsItemFromQueue(CancellationToken stoppingToken)
        {
            // This internal logic remains largely the same.
            RedisValue newsJson = await _redisDb.ListRightPopAsync(NewsQueueKey);
            if (!newsJson.HasValue)
            {
                return;
            }

            SystemNewsItem? newsItem = JsonSerializer.Deserialize<SystemNewsItem>(newsJson);
            if (newsItem == null)
            {
                return;
            }

            _logger.LogInformation("Processing news item '{Title}' from queue.", newsItem.Title);

            using IServiceScope scope = _serviceProvider.CreateScope();
            INotificationDispatchService dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatchService>();
            await dispatcher.DispatchNewsNotificationAsync(newsItem.Id, stoppingToken);
        }
    }

    // A simple DTO for news items in the queue
    public class SystemNewsItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string Title { get; set; }
        public required string Message { get; set; }
    }
}