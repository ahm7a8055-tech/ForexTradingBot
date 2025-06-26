// File: Infrastructure/DependencyInjection.cs

#region Usings
// --- Application ---
using Application.Common.Interfaces;
using Application.Common.Interfaces.CoinGeckoApiClient;
using Application.Common.Interfaces.Fred;
using Application.Features.Crypto.Services.CoinGecko;
using Application.Interfaces;
using Application.Services;

// --- Infrastructure ---
using Infrastructure.Caching;
using Infrastructure.ExternalServices;
using Infrastructure.Features.Forwarding.Extensions; // Assuming this is the correct namespace for your feature
using Infrastructure.Hangfire;
using Infrastructure.Persistence; // For DbConnectionFactory
using Infrastructure.Repositories;
using Infrastructure.Services;
using Infrastructure.Services.Admin;
using Infrastructure.Services.Fmp;

// --- Microsoft ---
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// --- Third-Party ---
using Hangfire;
using Hangfire.PostgreSql;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;
using Infrastructure.Persistence.Configurations;
using Polly.Retry;
using Shared.Maintenance;
using Hangfire.MemoryStorage;
#endregion

namespace Infrastructure.Data
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration, bool isSmokeTest)
        {
            // =================================================================
            // --- 1. DATABASE AND HANGFIRE CONFIGURATION ---
            // =================================================================
            #region Database and Hangfire Setup

            if (isSmokeTest)
            {
                // For smoke tests, use a fast, in-memory database to avoid dependencies on external infrastructure.
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("SmokeTestDatabase"));

                // ✅ UPGRADE: Added in-memory storage for Hangfire during smoke tests to avoid db dependency.
                services.AddHangfire(config => config.UseMemoryStorage());
            }
            else
            {
                // For real environments, read the provider and connection string from configuration.
                string? dbProvider = configuration.GetValue<string>("DatabaseSettings:DatabaseProvider")?.ToLowerInvariant();

                string? connectionString = configuration.GetConnectionString("DefaultConnection");

                // Fail-fast validation: Ensure critical settings are present.
                if (string.IsNullOrEmpty(dbProvider) || string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException(
                        "FATAL ERROR: The 'DatabaseSettings:DatabaseProvider' or 'ConnectionStrings:DefaultConnection' " +
                        "is missing in the configuration. The application cannot start without a configured database.");
                }

                // Use a switch statement to configure services that are specific to the database provider.
                switch (dbProvider)
                {
                    case "postgres":
                        // Configure EF Core (AppDbContext) for PostgreSQL.
                        services.AddDbContext<AppDbContext>(opts =>
                            opts.UseNpgsql(connectionString, npgsql =>
                                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

                        // Configure Hangfire to use PostgreSQL as its backing store.
                        services.AddHangfire(config => config
                            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
                        break;

                    case "sqlserver":
                        // Configure EF Core (AppDbContext) for SQL Server.
                        services.AddDbContext<AppDbContext>(opts =>
                            opts.UseSqlServer(connectionString, sql =>
                                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

                        // Configure Hangfire to use SQL Server as its backing store.
                        services.AddHangfire(config => config.UseSqlServerStorage(connectionString));
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported DatabaseProvider: '{dbProvider}'. Please use 'postgres' or 'sqlserver'.");
                }
            }

            // This original line registers the IHangfireCleaner.
            services.AddTransient<IHangfireCleaner, HangfireCleaner>();

            // ✅ UPGRADE: Re-registering with Scoped lifetime. Scoped is safer for services doing database work
            // within a request or job, ensuring they get fresh dependencies. This new registration
            // will be used by the DI container instead of the Transient one above for new resolutions.
            services.AddScoped<IHangfireCleaner, HangfireCleaner>();


            // Register the Scoped DbContext interface.
            services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

            #endregion

            // =================================================================
            // --- 2. DAPPER CONNECTION FACTORY REGISTRATION ---
            // =================================================================
            #region Dapper Connection Factory

            // This is the key to making Dapper repositories database-agnostic.
            services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

            #endregion

            // =================================================================
            // --- 3. CACHING AND EXTERNAL SERVICES ---
            // =================================================================
            #region Caching and External Services

            services.AddMemoryCache();
            services.AddSingleton(typeof(IMemoryCacheService<>), typeof(MemoryCacheService<>));
            string? redisConnectionString = configuration.GetConnectionString("Redis");
            if (string.IsNullOrEmpty(redisConnectionString))
            {
                throw new InvalidOperationException("FATAL ERROR: The 'Redis' connection string is missing.");
            }
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

            AsyncRetryPolicy<HttpResponseMessage> retryPolicy = HttpPolicyExtensions
               .HandleTransientHttpError()
               .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
               .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            services.AddHttpClient<ICryptoPayApiClient, CryptoPayApiClient>();
            services.Configure<RssReaderServiceSettings>(configuration.GetSection(RssReaderServiceSettings.ConfigurationSectionName));
            services.AddHttpClient(RssReaderService.HttpClientNamedClient, (serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<RssReaderServiceSettings>>().Value;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
            });
            services.AddHttpClient<ICoinGeckoApiClient, CoinGeckoApiClient>().AddPolicyHandler(retryPolicy);
            services.AddHttpClient<IFmpApiClient, FmpApiClient>().AddPolicyHandler(retryPolicy);
            services.AddHttpClient<IFredApiClient, FredApiClient>();

            #endregion

            // =================================================================
            // --- 4. APPLICATION SERVICES AND REPOSITORIES ---
            // =================================================================
            #region Application Services and Repositories

            services.AddHangfireServer();
            services.AddSingleton<ITelegramUserApiClient, TelegramUserApiClient>();

            // This line is redundant if TelegramUserApiClient is only resolved via its interface.
            // Leaving it in as requested. The DI will create a separate singleton instance for this registration.
            services.AddSingleton<TelegramUserApiClient>();

            services.AddHostedService<TelegramUserApiInitializationService>();

            // All these registrations are correct.
            services.AddScoped<IRssFetchingCoordinatorService, RssFetchingCoordinatorService>();
            services.AddScoped<IRssReaderService, RssReaderService>();
            services.AddTransient<INotificationJobScheduler, HangfireJobScheduler>();
            services.AddScoped<INotificationDispatchService, NotificationDispatchService>();
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<INewsItemRepository, NewsItemRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ITokenWalletRepository, TokenWalletRepository>();
            services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
            services.AddScoped<ISignalRepository, SignalRepository>();
            services.AddScoped<ISignalCategoryRepository, SignalCategoryRepository>();
            services.AddScoped<IRssSourceRepository, RssSourceRepository>();
            services.AddScoped<IUserSignalPreferenceRepository, UserSignalPreferenceRepository>();
            services.AddScoped<ISignalAnalysisRepository, SignalAnalysisRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();

            services.AddForwardingInfrastructure();

            #endregion

            return services;
        }
    }
}