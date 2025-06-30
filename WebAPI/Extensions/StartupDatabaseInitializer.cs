using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore; // مسیر AppDbContext شما

namespace WebAPI.Services;

public class StartupDatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StartupDatabaseInitializer> _logger;

    public StartupDatabaseInitializer(IServiceProvider serviceProvider, ILogger<StartupDatabaseInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Startup Database Initializer is starting.");

        // Create a new scope to retrieve scoped services
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                _logger.LogInformation("Applying database migrations asynchronously...");
                await dbContext.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Database migrations applied successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "A critical error occurred while applying database migrations.");
                // Optionally, you can decide to stop the application if the database is essential
                // var lifetime = _serviceProvider.GetRequiredService<IHostApplicationLifetime>();
                // lifetime.StopApplication();
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Startup Database Initializer is stopping.");
        return Task.CompletedTask;
    }
}