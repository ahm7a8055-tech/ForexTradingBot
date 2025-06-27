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
using System.Net.Http.Headers;
using System.Net;
using Hangfire.Storage.SQLite;
using Serilog;
using TL;
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
            services.AddSingleton<DbProviderService>();
            services.AddSingleton<UserSqlProvider>();
            if (isSmokeTest)
            {
                // For smoke tests, use a fast, in-memory database
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("SmokeTestDatabase"));

                // Use in-memory storage for Hangfire during smoke tests
                services.AddHangfire(config => config.UseMemoryStorage());
            }
            else // Not a smoke test, configure for real environments
            {
                string? dbProvider = configuration.GetValue<string>("DatabaseSettings:DatabaseProvider")?.ToLowerInvariant();
                string? connectionString = configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrEmpty(dbProvider) || string.IsNullOrEmpty(connectionString))
                {
                    Log.Warning("DatabaseProvider or DefaultConnection not found. Defaulting to local SQLite database.");
                    dbProvider = "sqlite";
                    connectionString = "Data Source=local_forex_bot.db";
                }

                // --- 1. Configure the Main DbContext First ---
                // We do this separately because a failure here should be fatal. The app can't run without its main DB.
                switch (dbProvider)
                {

                    case "sqlite":
                        services.AddDbContext<AppDbContext>(opts =>
                            opts.UseSqlite(connectionString, sqlite =>
                                sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

                        // --- ADDED: Hangfire config for SQLite ---
                        services.AddHangfire(config => config.UseSQLiteStorage(connectionString));
                        break;

                    case "postgres":
                    case "postgresql":
                        services.AddDbContext<AppDbContext>(opts =>
                            opts.UseNpgsql(connectionString, npgsql =>
                                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

                        // --- ADDED: Hangfire config for PostgreSQL ---
                        services.AddHangfire(config =>
                        {
                            config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString));
                            config.UseSerializerSettings(new Newtonsoft.Json.JsonSerializerSettings
                            {
                                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All
                            });
                        });
                        break;

                    case "sqlserver":
                        services.AddDbContext<AppDbContext>(opts =>
                            opts.UseSqlServer(connectionString, sql =>
                                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

                        // --- ADDED: Hangfire config for SQL Server ---
                        services.AddHangfire(config => config.UseSqlServerStorage(connectionString));
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported DatabaseProvider: '{dbProvider}'.");
            }

            // --- 2. Configure Hangfire with a Resilient Fallback ---
            services.AddHangfire(config =>
                {
                    config.UseSerializerSettings(new Newtonsoft.Json.JsonSerializerSettings
                    {
                        TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All
                    });

                    try
                    {
                        Log.Information("Attempting to configure Hangfire with '{DbProvider}' database storage.", dbProvider);
                        switch (dbProvider)
                        {
                            case "sqlite":
                                config.UseSQLiteStorage(connectionString);
                                Log.Information("✅ Hangfire successfully configured with SQLite storage.");
                                break;

                            case "postgres":
                            case "postgresql":
                                config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString));
                                Log.Information("✅ Hangfire successfully configured with PostgreSQL storage.");
                                break;

                            case "sqlserver":
                                config.UseSqlServerStorage(connectionString);
                                Log.Information("✅ Hangfire successfully configured with SQL Server storage.");
                                break;

                            default:
                                // This case should ideally not be hit due to the check above, but as a safeguard:
                                Log.Warning("Unsupported Hangfire DB provider '{DbProvider}'. Falling back to in-memory storage.", dbProvider);
                                config.UseMemoryStorage();
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // --- THIS IS THE FALLBACK LOGIC ---
                        Log.Error(ex, "FAILED to configure Hangfire with database storage. The database may be offline or the connection string is invalid. FALLING BACK TO IN-MEMORY STORAGE.");
                        Log.Warning("Hangfire jobs will NOT be persisted. They will be lost if the application restarts.");

                        // Re-configure with MemoryStorage on failure
                        config.UseMemoryStorage();
                    }
                });
            }

            // This original line registers the IHangfireCleaner.
            services.AddTransient<IHangfireCleaner, HangfireCleaner>();

            // ✅ UPGRADE: Re-registering with Scoped lifetime. Scoped is safer for services doing database work
            // within a request or job, ensuring they get fresh dependencies. This new registration
            // will be used by the DI container instead of the Transient one above for new resolutions.
            services.AddScoped<IHangfireCleaner, HangfireCleaner>();

            services.AddSingleton<UserSqlProvider>();
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
            if (!string.IsNullOrWhiteSpace(redisConnectionString))
            {
                try
                {

                    // Redis IS configured. Register the real ConnectionMultiplexer.
                    services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
                    Console.WriteLine("✅ Redis is configured and will be used for distributed caching and features."); // Using Console for early startup info
                }
                catch (RedisConnectionException ex)
                {
                    // If the connection string is present but invalid, it's a critical configuration error.
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"FATAL ERROR: Could not connect to Redis using the provided connection string. Please check the server and configuration. Error: {ex.Message}");
                    Console.ResetColor();
                    throw; // Fail fast if Redis is configured but unreachable.
                }
            }
            else
            {
                // Redis is NOT configured. We will log a warning and continue.
                // The application will run without distributed caching features.
                // We do NOT register IConnectionMultiplexer, so services requesting it will get null.
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️ Redis connection string not found. Caching will be in-memory and non-persistent. Distributed features like global image deduplication will be disabled.");
                Console.ResetColor();
            }
            AsyncRetryPolicy<HttpResponseMessage> retryPolicy = HttpPolicyExtensions
               .HandleTransientHttpError()
               .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
               .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

          
            services.Configure<RssReaderServiceSettings>(configuration.GetSection(RssReaderServiceSettings.ConfigurationSectionName));
            services.AddHttpClient(RssReaderService.HttpClientNamedClient, (serviceProvider, client) =>
            {
                // Configure default headers for every request made by this client.
                var settings = serviceProvider.GetRequiredService<IOptions<RssReaderServiceSettings>>().Value;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
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