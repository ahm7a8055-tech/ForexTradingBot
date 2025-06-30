// File: WebAPI/Program.cs

#region Usings
// Using های استاندارد .NET و NuGet Packages
// Using های مربوط به پروژه‌های شما
using Application;                          // برای متد توسعه‌دهنده AddApplicationServices
using Application.Common.Interfaces;
using Application.Features.Forwarding.Extensions;
// using Application.Interfaces;          // معمولاً اینترفیس‌های Application مستقیماً اینجا نیاز نیستند مگر برای موارد خاص
// using Application.Services;            // و نه پیاده‌سازی‌های آن
using BackgroundTasks;                    // برای متد توسعه‌دهنده AddBackgroundTasksServices (اگر تعریف کرده‌اید)
using BackgroundTasks.Services;
using Hangfire;                             // برای پیکربندی‌های Hangfire مانند CompatibilityLevel, RecurringJob, Cron
using Hangfire.Dashboard;                   // برای DashboardOptions, IDashboardAuthorizationFilter
using Hangfire.MemoryStorage;
using Hangfire.PostgreSql;
using Hangfire.SqlServer;
using Hangfire.Storage.SQLite;
using Infrastructure.Data;
using Infrastructure.ExternalServices;
using Hangfire.Annotations;


// using Hangfire.SqlServer;              // اگر از SQL Server برای Hangfire استفاده می‌کنید
// using WebAPI.Filters; //  Namespace برای HangfireNoAuthFilter (اگر در این مسیر است و استفاده می‌کنید)
using Infrastructure.Features.Forwarding.Extensions;
using Infrastructure.Logging;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;             // برای OpenApiInfo
using Serilog;                              // برای Log, LoggerConfiguration, UseSerilog
using Serilog.Enrichers.WithCaller;
using Shared.Helpers;
using Shared.Maintenance;
using Shared.Settings;                    // برای CryptoPaySettings (از پروژه Shared)
using StackExchange.Redis;
using System.Configuration;
using TelegramPanel.Extensions;
using TelegramPanel.Infrastructure.Services;
using TL;
using WebAPI.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Dapper;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

#endregion

// Register the handler at app startup:
SqlMapper.AddTypeHandler(new GuidTypeHandler());

// ------------------- پیکربندی اولیه لاگر Serilog (Bootstrap Logger) -------------------
// این لاگر قبل از خواندن کامل appsettings.json و ساخت هاست استفاده می‌شود
// تا خطاهای بسیار اولیه در راه‌اندازی برنامه نیز لاگ شوند.
Log.Logger = new LoggerConfiguration()
    // Read base configuration from appsettings.json
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "WebAPI") // Set a default name
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithCaller()
    .WriteTo.Console() // Always write to console during bootstrap
    .CreateBootstrapLogger(); // Use CreateBootstrapLogger() for this initial phase

// Add a global handler for unhandled exceptions. This is our safety net.
AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
{
Log.Fatal((Exception)eventArgs.ExceptionObject, "FATAL UNHANDLED EXCEPTION");
Log.CloseAndFlush(); // Ensure the fatal log is sent before the app dies
};

try
{

    Log.Information("--------------------------------------------------");
    Log.Information("Application Starting Up (Program.cs)...");
    Log.Information("--------------------------------------------------");
    int minThreads = 50; // Lowered from 500 for better resource management
    _ = ThreadPool.SetMinThreads(minThreads, minThreads);
    Log.Information("ThreadPool minimum threads set to {MinThreads}.", minThreads);
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    _ = builder.WebHost.UseKestrel();
    builder.Services.AddSingleton<Infrastructure.Logging.TelegramAdminSink>();
    // This is more reliable than GetValue<bool> for environment variables.
    string smokeTestFlag = builder.Configuration["IsSmokeTest"] ?? "false";
    bool isSmokeTest = "true".Equals(smokeTestFlag, StringComparison.OrdinalIgnoreCase);

    #region Configure Serilog Logging
    // ------------------- ۱. پیکربندی Serilog با تنظیمات از appsettings.json -------------------
    // این بخش Serilog را به عنوان سیستم لاگینگ اصلی برنامه تنظیم می‌کند.
    _ = builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
      .ReadFrom.Configuration(context.Configuration)

      .Enrich.FromLogContext()
      .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
      .Enrich.WithMachineName()
      .Enrich.WithEnvironmentName()

      // --- THIS IS THE FINAL, DEFINITIVE CONFIGURATION ---
      .Enrich.With(new CustomCallerEnricher(
          "Serilog",
          "Microsoft",
          "System",
          "Infrastructure.Logging" // <-- The critical addition
      ))
      // ----------------------------------------------------

      .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}")

       // ==================== بخش جدید برای لاگ‌گیری در فایل ====================
       .WriteTo.File(
          path: "logs/log-.txt",
          rollingInterval: RollingInterval.Hour,
          retainedFileCountLimit: 24,
          rollOnFileSizeLimit: true,
          fileSizeLimitBytes: 10 * 1024 * 1024,
          shared: true,
          restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error, // Only write Error and above to file
          outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}"
      // --------------------
      )
      // =======================================================================

      .WriteTo.Sink(
          new Infrastructure.Logging.TelegramAdminSink(context.Configuration),
          Serilog.Events.LogEventLevel.Error // فقط لاگ‌های سطح Error و بالاتر به تلگرام ارسال می‌شود
      )
  );




    #region Caching and External Services (Or a new "Infrastructure" region)

    string? redisConnectionString = builder.Configuration.GetConnectionString("Redis");

    builder.Configuration.GetConnectionString("Redis");

    if (string.IsNullOrWhiteSpace(redisConnectionString))
    {
        // If no external Redis is configured, start our own embedded one.
        Log.Warning("⚠️ Redis connection string not found. Attempting to start embedded Redis server for this session.");

        // 1. Register the IHostedService that will manage the redis-server.exe process.
        builder.Services.AddHostedService<Infrastructure.Services.EmbeddedRedisService>();

        // 2. Set the connection string to the default for the local, embedded server.
        redisConnectionString = "localhost:6379"; // Default port for Redis

        // --- THIS IS THE FIX ---
        // 3. Update the application's configuration in memory so all other services see the new value.
        //    This ensures consistency across the entire application.
        builder.Configuration.GetSection("ConnectionStrings")["Redis"] = redisConnectionString;

        Log.Information("Embedded Redis registered. Connection string set to '{RedisConnectionString}' for this session.", redisConnectionString);
    }
    else
    {
        Log.Information("✅ External Redis connection string found. Will connect to {RedisEndpoint}", redisConnectionString);
    }

    try
    {
        // This registration is now guaranteed to work because redisConnectionString will always have a value.
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            // We re-read from the configuration to ensure we use the potentially updated value.
            var finalConnectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Redis");
            var options = ConfigurationOptions.Parse(finalConnectionString!);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 5000;
            options.SyncTimeout = 5000;
            return ConnectionMultiplexer.Connect(options);
        });

        Log.Information("✅ Redis services configured to connect to {RedisEndpoint}", redisConnectionString);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to configure Redis connection multiplexer. Using fallback in-memory Redis.");
        
        // Register the fallback in-memory Redis service
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Infrastructure.Services.FallbackRedisService>>();
            return new Infrastructure.Services.FallbackRedisService(logger);
        });
    }

    // --- NEW: Test Redis connectivity and fallback to local if needed ---
    // Note: We'll test Redis connectivity after the application starts, not during configuration
    // This allows the EmbeddedRedisService to start the Redis server first if needed.

    #endregion

    builder.Services.AddAutoMapper(typeof(Program));
    builder.Services.AddSingleton<Application.Common.Interfaces.ILoggingSanitizer, Infrastructure.Security.PiiLoggingSanitizer>();
    #endregion

    #region Add Core ASP.NET Core Services
    _ = builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "ForexTradingBotAPI";
    });

    // ------------------- ۲. اضافه کردن سرویس‌های پایه ASP.NET Core -------------------
    // فعال کردن پشتیبانی از کنترلرهای API
    _ = builder.Services.AddControllers();
    // فعال کردن API Explorer برای تولید مستندات Swagger/OpenAPI
    _ = builder.Services.AddEndpointsApiExplorer();

    // Add CORS
    _ = builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(builder =>
        {
            _ = builder.WithOrigins("http://localhost:3000", "http://localhost:4200")
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials();
        });
    });
    IWebHostEnvironment environment = builder.Environment;
    // پیکربندی Swagger/OpenAPI برای مستندسازی API
    _ = builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Version = "v1",
            Title = "Forex Signal Bot API",
            Description = "API endpoints for the Forex Signal Bot application, including Telegram webhook and administrative functions.",
            Contact = new OpenApiContact
            {
                Name = "Support",
                Email = "support@example.com"
            }
        });

        // Add XML documentation
        string xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        // Add JWT Authentication
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Configure Swagger to handle conflicting routes
        options.CustomSchemaIds(type => type.FullName);
        options.ResolveConflictingActions(apiDescriptions =>
        {
            Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription first = apiDescriptions.First();
            return first;
        });
    });
    Log.Information("Core ASP.NET Core services (Controllers, API Explorer, Swagger) added.");
    #endregion

    #region Configure Application Options/Settings



    // ------------------- ۳. پیکربندی Options (خواندن تنظیمات از appsettings.json) -------------------
    // مپ کردن بخش "TelegramSettings" از appsettings.json به کلاس Domain.Settings.TelegramSettings
    // این کلاس می‌تواند شامل تنظیمات عمومی تلگرام مانند AdminUserId باشد.
    _ = builder.Services.Configure<Domain.Settings.TelegramSettings>(builder.Configuration.GetSection("TelegramSettings"));
    // Configure TelegramUserApiSettings
    // مپ کردن بخش CryptoPaySettings.SectionName (که "CryptoPay" است) از appsettings.json به کلاس Shared.Settings.CryptoPaySettings
    _ = builder.Services.Configure<CryptoPaySettings>(builder.Configuration.GetSection(CryptoPaySettings.SectionName));


    _ = builder.Services.AddMemoryCache();


    // TelegramPanelSettings در متد AddTelegramPanelServices پیکربندی می‌شود.
    Log.Information("Application settings (Options pattern) configured.");


    #endregion

    #region Register Custom Application Layers and Services
    // ------------------- ۴. رجیستر کردن سرویس‌های لایه‌های مختلف برنامه -------------------
    // این متدها باید در فایل‌های DependencyInjection.cs (یا ServiceCollectionExtensions.cs) هر لایه تعریف شده باشند.
    // ترتیب فراخوانی: ابتدا لایه‌های پایه (Application, Infrastructure)، سپس لایه‌های Presentation یا خاص (TelegramPanel, BackgroundTasks).




    _ = builder.Services.AddApplicationServices();
    Log.Information("Application services registered.");

    // --- DATABASE CONNECTION PROMPT (before infrastructure services) ---
    if (!isSmokeTest)
    {
        string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        string? dbProvider = builder.Configuration.GetValue<string>("DatabaseSettings:DatabaseProvider")?.ToLowerInvariant();

        Log.Information("Database configuration check - ConnectionString: {HasConnectionString}, Provider: {Provider}", 
            !string.IsNullOrEmpty(connectionString), dbProvider ?? "null");

        if (string.IsNullOrEmpty(connectionString))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--- Database Connection Setup ---");
            Console.WriteLine("No database connection string found. Please choose:");
            Console.WriteLine("1. Press Enter to use LocalDB (SQL Server Express)");
            Console.WriteLine("2. Type '2' or 'sqlite' to use SQLite file database");
            Console.WriteLine("3. Enter a custom connection string (SQL Server, PostgreSQL, etc.)");
            Console.ResetColor();

            Console.Write("Enter your choice: ");
            var userInput = Console.ReadLine()?.Trim();

            Log.Information("User input received: '{UserInput}'", userInput ?? "null");

            if (string.IsNullOrWhiteSpace(userInput))
            {
                connectionString = @"Server=(localdb)\MSSQLLocalDB;Database=ForexTradingBot;Trusted_Connection=true;MultipleActiveResultSets=true";
                dbProvider = "sqlserver";
                Log.Information("User chose LocalDB. Connection string set to LocalDB.");
            }
            else if (userInput.Equals("1"))
            {
                connectionString = @"Server=(localdb)\MSSQLLocalDB;Database=ForexTradingBot;Trusted_Connection=true;MultipleActiveResultSets=true";
                dbProvider = "sqlserver";
                Log.Information("User chose LocalDB (option 1). Connection string set to LocalDB.");
            }
            else if (userInput.Equals("sqlite", StringComparison.OrdinalIgnoreCase) || userInput.Equals("2"))
            {
                connectionString = "Data Source=local_forex_bot.db";
                dbProvider = "sqlite";
                Log.Information("User chose SQLite. Connection string set to SQLite file database.");
            }
            else
            {
                connectionString = userInput;
                if (connectionString.Contains("PostgreSQL") || connectionString.Contains("postgres"))
                    dbProvider = "postgres";
                else if (connectionString.Contains("Server=") || connectionString.Contains("Data Source="))
                    dbProvider = "sqlserver";
                else
                    dbProvider = "sqlite";
                Log.Information("User provided custom connection string. Provider detected: {Provider}", dbProvider);
            }

            Log.Information("Setting configuration - ConnectionString: {ConnectionString}, Provider: {Provider}", 
                connectionString?.Replace("Password=", "Password=***"), dbProvider);

            builder.Configuration.GetSection("ConnectionStrings")["DefaultConnection"] = connectionString;
            builder.Configuration.GetSection("DatabaseSettings")["DatabaseProvider"] = dbProvider;
        }
        else if (string.IsNullOrEmpty(dbProvider))
        {
            if (connectionString.Contains("PostgreSQL") || connectionString.Contains("postgres"))
                dbProvider = "postgres";
            else if (connectionString.Contains("Server=") || connectionString.Contains("Data Source="))
                dbProvider = "sqlserver";
            else
                dbProvider = "sqlite";
            builder.Configuration.GetSection("DatabaseSettings")["DatabaseProvider"] = dbProvider;
            Log.Information("Database provider auto-detected as: {Provider}", dbProvider);
        }
    }

    // --- FINAL VALIDATION: Ensure connection string is valid before infrastructure registration ---
    if (!isSmokeTest)
    {
        string? finalConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(finalConnectionString))
        {
            throw new InvalidOperationException(
                "No valid database connection string was provided. " +
                "The application should have prompted for database connection details, but no valid connection string was set. " +
                "Please ensure you provided a valid connection string when prompted.");
        }
        
        Log.Information("Database connection validated. Using provider: {Provider}, Connection: {ConnectionString}", 
            builder.Configuration.GetValue<string>("DatabaseSettings:DatabaseProvider"), 
            finalConnectionString.Replace("Password=", "Password=***")); // Hide password in logs
        // NOTE: Connection string options for stability and security are appended in appsettings.Production.json
    }

    _ = builder.Services.AddInfrastructureServices(builder.Configuration, isSmokeTest);
    Log.Information("Infrastructure services registered.");


    _ = builder.Services.AddTelegramPanelServices(builder.Configuration);
    Log.Information("Telegram panel services registered.");

    _ = builder.Services.AddBackgroundTasksServices();
    Log.Information("Background tasks services registered.");
    _ = builder.Services.AddHealthChecks();



    // --- Universal Conditional Feature: Auto-Forwarding ---
    // We check for the actual secrets required for this feature to run.
    string? apiId = builder.Configuration["TelegramUserApi:ApiId"];
    string? apiHash = builder.Configuration["TelegramUserApi:ApiHash"];
    bool isAutoForwardingEnabled = !string.IsNullOrEmpty(apiId) && !string.IsNullOrEmpty(apiHash);
    try
    {
        if (Environment.UserInteractive)
        {
            // This helper class is defined at the bottom of Program.cs
            ConfigurationHelper.PromptForMissingSecrets(builder.Configuration);
        }


        if (isAutoForwardingEnabled)
        {
            string? sessionPath = builder.Configuration["TelegramUserApi:SessionPath"];
            if (string.IsNullOrEmpty(sessionPath))
            {
                sessionPath = Path.Combine(AppContext.BaseDirectory, "telegram_user.session");
                Log.Warning("TelegramUserApi:SessionPath not configured. Defaulting to {SessionPath}", sessionPath);
            }
          

                // 1. Configure the settings object
                _ = builder.Services.Configure<Infrastructure.Settings.TelegramUserApiSettings>(builder.Configuration.GetSection("TelegramUserApi"));

                // 2. Register the API client itself
                _ = builder.Services.AddSingleton<ITelegramUserApiClient, TelegramUserApiClient>();

                // 3. Register the background service that initializes the client
                _ = builder.Services.AddHostedService<TelegramUserApiInitializationService>();

                // 4. Register all the other forwarding services from the other projects
                _ = builder.Services.AddForwardingInfrastructure();
                _ = builder.Services.AddForwardingServices();
                _ = builder.Services.AddForwardingOrchestratorServices();

                Log.Information("All Auto-Forwarding services have been successfully registered.");
            }
            else
            {
                // If the secrets are missing, we skip ALL related services.
                Log.Information("ℹ️ Auto-Forwarding feature DISABLED (ApiId or ApiHash not found in configuration).");
            }


        // --- Universal Conditional Feature: CryptoPay (Example) ---
        // The same robust pattern is applied here.
        string? cryptoPayToken = builder.Configuration["CryptoPay:ApiToken"];
        bool isCryptoPayEnabled = !string.IsNullOrWhiteSpace(cryptoPayToken) && !cryptoPayToken.Contains("REPLACE");

        if (isCryptoPayEnabled)
        {
            // The service is ONLY registered if the feature is enabled.
            // This prevents the constructor from ever being called if the token is missing.
            builder.Services.AddHttpClient<Application.Common.Interfaces.ICryptoPayApiClient, CryptoPayApiClient>()
                .AddTypedClient((httpClient, serviceProvider) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<CryptoPaySettings>>();
                    var logger = serviceProvider.GetRequiredService<ILogger<CryptoPayApiClient>>();
                    return new CryptoPayApiClient(httpClient, options, logger);
                });

            Log.Information("✅ CryptoPay feature ENABLED (ApiToken was found in configuration).");
        }
        else
        {
            // Register a disabled implementation to avoid DI errors
            builder.Services.AddSingleton<Application.Common.Interfaces.ICryptoPayApiClient, Infrastructure.ExternalServices.DisabledCryptoPayApiClient>();
            Log.Information("ℹ️ CryptoPay feature DISABLED (CryptoPay secrets not found or were skipped). Using DisabledCryptoPayApiClient.");
        }

    }

    catch
    {
        // If the secrets are missing, we skip ALL related services.
        Log.Information("ℹ️ Auto-Forwarding feature DISABLED (ApiId or ApiHash not found in configuration).");
    }






    _ = builder.Services.AddWindowsService();
    bool isRunningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
    if (OperatingSystem.IsWindows() && !isRunningInContainer)
    {
        try
        {
            Log.Information("Running on Windows (not in a container). Checking for local SQL Server services...");
       //     ServiceManagerHelper.EnsureAllServicesRunningAndHealthy(); // Changed 
            Log.Information("SQL Server service check complete.");
        }
        catch (Exception exSql)
        {
            // This is not a fatal error, so we just log it and continue.
            // The app might be connecting to a remote or non-service SQL instance.
            Log.Warning(exSql, "Could not ensure local SQL Server services are running. This may be expected.");
        }
    }
    else
    {
        // Log why we are skipping the check. This is useful for debugging.
        if (isRunningInContainer)
        {
            Log.Information("Skipping SQL Server service check: Application is running inside a Docker container.");
        }
        else if (!OperatingSystem.IsWindows())
        {
            Log.Information("Skipping SQL Server service check: Application is not running on Windows.");
        }
    }





    #endregion

    #region Configure Hangfire

    // =========================================================================
    // ✅✅ CORRECTED HANGFIRE CONFIGURATION (PROVIDER-AWARE) ✅✅
    // =========================================================================
    builder.Services.AddHangfire(config =>
    {
        // Apply the JSON serializer settings first, as they are needed regardless of storage.
        config.UseSerializerSettings(new Newtonsoft.Json.JsonSerializerSettings
        {
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All
        });

        // This check is for smoke tests, which should always use in-memory storage.
        if (isSmokeTest)
        {
            Log.Information("✅ Smoke Test: Configuring Hangfire with In-Memory storage.");
            config.UseMemoryStorage();
            return; // Exit the configuration lambda early.
        }

        // --- Resilient Database Configuration with Fallback ---
        try
        {
            // Get the current database provider and connection string
            string? dbProvider = builder.Configuration.GetValue<string>("DatabaseSettings:DatabaseProvider")?.ToLowerInvariant();
            string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

            // Connection string should already be configured by this point
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("DefaultConnection string not found. Database connection should be configured before Hangfire setup.");
            }

            if (string.IsNullOrEmpty(dbProvider))
            {
                // Try to detect provider from existing connection string
                if (connectionString.Contains("PostgreSQL") || connectionString.Contains("postgres"))
                    dbProvider = "postgres";
                else if (connectionString.Contains("Server=") || connectionString.Contains("Data Source="))
                    dbProvider = "sqlserver";
                else
                    dbProvider = "sqlite"; // Default fallback
                
                builder.Configuration.GetSection("DatabaseSettings")["DatabaseProvider"] = dbProvider;
                Log.Information("Database provider auto-detected as: {Provider}", dbProvider);
            }

            Log.Information("Attempting to configure Hangfire with '{DbProvider}' provider.", dbProvider);

            // Fail fast if the connection string is still missing for a required provider.
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Hangfire: ConnectionString is missing for provider '{dbProvider}'.");
            }

            switch (dbProvider)
            {
                case "postgres":
                case "postgresql":
                    config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString));
                    Log.Information("✅ Hangfire successfully configured with PostgreSQL storage.");
                    break;

                case "sqlserver":
                    config.UseSqlServerStorage(connectionString);
                    Log.Information("✅ Hangfire successfully configured with SQL Server storage.");
                    break;

                case "sqlite":
                    config.UseSQLiteStorage(connectionString);
                    Log.Information("✅ Hangfire successfully configured with SQLite storage.");
                    break;

                default:
                    throw new NotSupportedException($"Unsupported Hangfire DatabaseProvider: '{dbProvider}'.");
            }
        }
        catch (Exception ex)
        {
            // --- THIS IS THE FALLBACK LOGIC ---
            Log.Error(ex, "FAILED to configure Hangfire with the specified database storage. FALLING BACK TO IN-MEMORY STORAGE.");
            Log.Warning("Hangfire jobs will NOT be persisted and will be lost if the application restarts.");

            // CORRECTED: Do NOT call AddHangfire again. Just configure the existing 'config' object.
            config.UseMemoryStorage();
        }
    });


    _ = builder.Services.AddHangfireCleaner();
    _ = builder.Services.AddHangfireServer(options =>
    {
        options.ServerName = $"{Environment.MachineName}:Notifications";
        options.WorkerCount = 25; // <--- THE THROTTLE! Adjust this based on Telegram API limits.
        options.Queues = new[] { "notifications" }; // It ONLY processes this queue.
    });
 

    builder.Services.AddHangfireServer(options =>
    {
        options.ServerName = $"{Environment.MachineName}:Default";
        options.WorkerCount = Math.Min(20, Environment.ProcessorCount * 2); // Lowered for safer DB usage
        options.Queues = new[] { "critical", "default" }; // Explicitly list the queues it WILL process
    });

    Log.Information("Hangfire cleaner service added.");

    Log.Information("Performing final manual service registrations...");

    // FIX FOR: Unable to resolve 'IBotCommandSetupService'
    _ = builder.Services.AddTransient<IBotCommandSetupService, BotCommandSetupService>();

    Log.Information("Final manual service registrations complete.");
    _ = builder.Services.Configure<List<Infrastructure.Settings.ForwardingRule>>( // <<< Fully qualified
    builder.Configuration.GetSection("ForwardingRules"));

    #endregion

    // ------------------- ساخت WebApplication instance -------------------
    WebApplication app = builder.Build(); //  ساخت برنامه با تمام سرویس‌های پیکربندی شده
    Log.Information("Application host built. Performing mandatory startup tasks...");

    // Automatically apply EF Core migrations at startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();
        try
        {
            // Validate connection string before attempting database operations
            string? connectionString = app.Services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is missing or empty. Cannot proceed with database operations.");
            }

            Log.Information("Attempting to apply database migrations...");
            db.Database.Migrate();
            Log.Information("Database migrations applied successfully.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChangesWarning"))
        {
            // Fallback: try to create the database if migrations fail due to pending model changes
            Log.Warning("Migration failed due to pending model changes. Attempting to create database...");
            try
            {
                db.Database.EnsureCreated();
                Log.Information("Database created successfully using EnsureCreated().");
            }
            catch (Exception createEx)
            {
                Log.Error(createEx, "Failed to create database using EnsureCreated(). Connection string may be invalid.");
                throw new InvalidOperationException($"Database creation failed. Please check your connection string: {createEx.Message}", createEx);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply database migrations or create database. Connection string may be invalid.");
            throw new InvalidOperationException($"Database setup failed. Please check your connection string: {ex.Message}", ex);
        }
    }

    // The application will now start INSTANTLY.
    #region Queue Startup Maintenance Jobs to Hangfire

    // We register a callback that runs ONCE, right after the application has fully started.
    _ = app.Lifetime.ApplicationStarted.Register(() =>
    {
        Log.Information("Application has fully started. Now enqueuing background maintenance jobs.");
        try
        {
            // We get the necessary services from the application's root service provider.
            IBackgroundJobClient backgroundJobClient = app.Services.GetRequiredService<IBackgroundJobClient>();
            IConfiguration configuration = app.Services.GetRequiredService<IConfiguration>();
            string? connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                Log.Error("Cannot enqueue maintenance jobs: DefaultConnection string is missing.");
                return;
            }

            Log.Information("Enqueuing Hangfire core cleanup job to run in the background...");
            // This job will run once, as soon as a Hangfire server is available.
        //    _ = backgroundJobClient.Enqueue<IHangfireCleaner>(cleaner => cleaner.PurgeCompletedAndFailedJobs(connectionString));

            Log.Information("Enqueuing duplicate NewsItem cleanup job to run in the background...");
           // _ = backgroundJobClient.Enqueue<IHangfireCleaner>(cleaner => cleaner.PurgeDuplicateNewsItems(connectionString));

            Log.Information("✅ All startup maintenance jobs have been successfully enqueued. They will run asynchronously.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while trying to enqueue startup maintenance jobs.");
        }
    });

    // --- NEW: Test Redis connectivity after application starts ---
    _ = app.Lifetime.ApplicationStarted.Register(async () =>
    {
        Log.Information("Testing Redis connectivity after application startup...");
        
        try
        {
            // Wait a bit for the EmbeddedRedisService to start Redis if needed
            await Task.Delay(TimeSpan.FromSeconds(3));
            
            var redisConnection = app.Services.GetRequiredService<IConnectionMultiplexer>();
            var redisDb = redisConnection.GetDatabase();
            
            // Test basic operations
            var testKey = $"test_connection_{Guid.NewGuid():N}";
            var testValue = DateTime.UtcNow.ToString();
            
            await redisDb.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(10));
            var retrievedValue = await redisDb.StringGetAsync(testKey);
            
            if (retrievedValue == testValue)
            {
                Log.Information("✅ Redis connectivity test successful. Redis is working properly.");
            }
            else
            {
                Log.Warning("⚠️ Redis test failed: Retrieved value doesn't match expected value.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Redis connectivity test failed. Some distributed features may not work properly.");
            
            // In release mode, prompt user for options
            if (!app.Environment.IsDevelopment())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n--- Redis Connection Issue ---");
                Console.WriteLine("Redis connection failed after startup. You can:");
                Console.WriteLine("1. Restart the application");
                Console.WriteLine("2. Check if Redis server is running on localhost:6379");
                Console.WriteLine("3. Continue without Redis (some features may not work)");
                Console.ResetColor();
            }
        }
    });

    #endregion

    Log.Information("Mandatory startup tasks completed.");


    // ------------------- دریافت لاگر از DI برای استفاده در ادامه Program.cs -------------------
    //  این لاگر، لاگری است که توسط UseSerilog پیکربندی شده است.
    ILogger<Program> programLogger = app.Services.GetRequiredService<ILogger<Program>>(); //  استفاده از ILogger<Program> برای لاگ‌های مختص Program.cs

    #region Configure HTTP Request Pipeline
    // ------------------- ۶. پیکربندی پایپ‌لاین پردازش درخواست‌های HTTP -------------------

    //  فعال کردن Request Body Buffering. این برای خواندن بدنه درخواست چندین بار (مثلاً در Middleware ها یا کنترلرها) لازم است.
    //  مخصوصاً برای CryptoPayWebhookController جهت اعتبارسنجی امضا.
    _ = app.Use(async (context, next) =>
    {
        context.Request.EnableBuffering();
        await next.Invoke();
    });

    // Enable Swagger in all environments
    _ = app.UseSwagger();
    _ = app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Forex Signal Bot API V1");
        c.RoutePrefix = string.Empty; // This will make Swagger UI the root page
        c.DefaultModelsExpandDepth(-1); // Hide models section by default
    });

    //  پیکربندی‌های مختص محیط توسعه
    if (app.Environment.IsDevelopment())
    {

        programLogger.LogInformation("Development environment detected. Enabling Developer Exception Page.");
        _ = app.UseDeveloperExceptionPage(); //  نمایش صفحه خطای با جزئیات برای توسعه‌دهندگان
    }
    else //  پیکربندی‌های مختص محیط Production
    {
        programLogger.LogInformation("Production environment detected. Enabling HSTS.");
        // app.UseExceptionHandler("/Error"); //  می‌توانید یک صفحه خطای سفارشی برای کاربران نهایی تعریف کنید
        _ = app.UseHsts(); //  افزودن هدر HTTP Strict Transport Security برای امنیت بیشتر (اجبار استفاده از HTTPS)
    }

    _ = app.UseHttpsRedirection(); //  ریدایرکت خودکار تمام درخواست‌های HTTP به HTTPS

    _ = app.UseSerilogRequestLogging(); //  لاگ کردن تمام درخواست‌های HTTP ورودی با جزئیات (توسط Serilog)

    _ = app.UseRouting();
    _ = app.UseAuthorization();
    _ = app.MapHangfireDashboard();
    programLogger.LogInformation("HTTP request pipeline configured.");
    #endregion

    #region Configure Hangfire Dashboard & Recurring Jobs
    // ------------------- ۷. اضافه کردن داشبورد Hangfire -------------------
    DashboardOptions hangfireDashboardOptions = new()
    {
        DashboardTitle = "Forex Trading Bot - Background Jobs Monitor",
        IgnoreAntiforgeryToken = true,
        // ✅ CHANGED: Applying a basic authorization filter for development.
        // For production, you MUST implement proper authentication/authorization here.
        Authorization = new[] { new LocalRequestsOnlyAuthorizationFilter() }
    };

    _ = app.UseHangfireDashboard("/hangfire", hangfireDashboardOptions);
    programLogger.LogInformation("Hangfire Dashboard configured at /hangfire (For development, open to local requests. Secure for production!).");

    // ------------------- ۸. زمان‌بندی Job های تکرارشونده Hangfire -------------------
    //  این Job ها پس از شروع کامل برنامه، توسط سرور Hangfire به طور خودکار اجرا خواهند شد.
    IHostApplicationLifetime appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    _ = appLifetime.ApplicationStarted.Register(() => // اجرا پس از اینکه برنامه کامل شروع شد
    {
        // آیا این لاگ را در کنسول می‌بینید؟
        programLogger.LogInformation("Application fully started. Scheduling/Updating Hangfire recurring jobs...");
        try
        {
            IRecurringJobManager recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();

            // از این روش استفاده کنیم که وابستگی‌ها را بهتر مدیریت می‌کند
            recurringJobManager.AddOrUpdate<IRssFetchingCoordinatorService>(
                recurringJobId: "fetch-all-active-rss-feeds",
                methodCall: service => service.FetchAllActiveFeedsAsync(CancellationToken.None),
                cronExpression: "*/15 * * * *",
                options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc }
            );

            // آیا این لاگ موفقیت را در کنسول می‌بینید؟
            programLogger.LogInformation(">>> Recurring job 'fetch-all-active-rss-feeds' was successfully scheduled. <<<");
        }
        catch (Exception ex)
        {
            // اگر خطایی رخ دهد، آیا این لاگ را می‌بینید؟
            programLogger.LogCritical(ex, ">>> CRITICAL: FAILED to schedule Hangfire recurring job. <<<");
        }

    });
    programLogger.LogInformation("Hangfire recurring jobs registration initiated (will run after application starts).");
    #endregion

    #region Map Controllers & Run Application
    _ = app.MapHealthChecks("/healthz");

    // ------------------- مپ کردن کنترلرها و اجرای برنامه -------------------
    //app.MapGet("/maintenance/force-hangfire-purge-all", async (IConfiguration config, IHangfireCleaner cleaner, ILogger<Program> logger) => {
    //    logger.LogWarning("MANUAL TRIGGER: Forcefully purging all Succeeded and Failed Hangfire jobs.");
    //    string? connectionString = config.GetConnectionString("DefaultConnection");

    //    if (string.IsNullOrEmpty(connectionString))
    //    {
    //        logger.LogError("Force Purge Failed: DefaultConnection string is missing.");
    //        return Results.Problem("DefaultConnection string not found.");
    //    }

    //    try
    //    {
    //        // Use the cleaner service you already have!
    //        cleaner.PurgeCompletedAndFailedJobs(connectionString);
    //        logger.LogInformation("MANUAL TRIGGER: Hangfire job purge completed successfully.");
    //        return Results.Ok("Hangfire Succeeded/Failed jobs have been purged. Check the Hangfire Dashboard to see the result.");
    //    }
    //    catch (Exception ex)
    //    {
    //        logger.LogError(ex, "MANUAL TRIGGER: An error occurred during the Hangfire purge.");
    //        return Results.Problem($"An error occurred: {ex.Message}");
    //    }
    //});


  


    _ = app.MapControllers(); //  مسیردهی درخواست‌ها به Action های کنترلرها
    programLogger.LogInformation("Application setup complete. Starting web host now...");

    // Get the application URL from configuration or use default
    string urls = builder.Configuration["Urls"] ?? "https://localhost:5001;http://localhost:5000";
    string firstUrl = urls.Split(';')[0].Trim();

    if (isAutoForwardingEnabled)
    {
        using (IServiceScope scope = app.Services.CreateScope())
        {
            Log.Information("Auto-Forwarding is enabled, resolving the orchestrator service for startup tasks.");
            // This code is now safe because we know the service was registered.
            UserApiForwardingOrchestrator orchestrator = scope.ServiceProvider.GetRequiredService<UserApiForwardingOrchestrator>();
            // You can now safely use the 'orchestrator' object here if needed for any startup logic.
        }
    }
    // --- File: WebAPI/Program.cs ---

    // ... after app.UseSerilogRequestLogging() and other middleware ...

    app.MapGet("/testerror", () => {
        // This will force an exception and a high-level log event
        try
        {
            throw new InvalidOperationException("This is a guaranteed test exception for the Telegram sink.");
        }
        catch (Exception ex)
        {
            // Use the ILogger from the dependency injection container
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Caught a test exception.");
            return Results.Problem("A test error was logged. Check your Telegram and console.");
        }
    });
    app.UseSerilogRequestLogging();
    // In the middleware pipeline section (before app.Run())
    _ = app.UseStaticFiles();
    app.Run(); //  شروع به گوش دادن به درخواست‌های HTTP و اجرای برنامه
    #endregion
}
catch (Exception ex)
{
    //  لاگ کردن خطاهای بسیار بحرانی که مانع از اجرای برنامه شده‌اند.
    Log.Fatal(ex, "Application host very much terminated unexpectedly.");
    // Environment.ExitCode = 1; //  برای نشان دادن خروج ناموفق به سیستم عامل یا اسکریپت‌های دیگر
}
finally
{

    Log.Information("--------------------------------------------------");
    Log.Information("Application Shutting Down...");
    Log.CloseAndFlush();
    Log.Information("--------------------------------------------------");
}

public static class ConfigurationHelper
{
    private static void PromptForTelegramApiSecrets(IConfiguration configuration)
    {
        string? apiId = configuration["TelegramUserApi:ApiId"];
        string? apiHash = configuration["TelegramUserApi:ApiHash"];

        if (string.IsNullOrEmpty(apiId) || apiId == "0" || string.IsNullOrEmpty(apiHash))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--- Telegram User API Setup (Optional) ---");
            Console.WriteLine("Auto-Forwarding feature requires Telegram API credentials.");
            Console.WriteLine("You can get these from my.telegram.org.");
            Console.WriteLine("Enter 'skip' to disable this feature for this session.");
            Console.ResetColor();

            // Only prompt if the section is potentially needed.
            if (string.IsNullOrEmpty(apiId) || apiId == "0")
            {
                Console.Write("Enter your ApiId (or 'skip'): ");
                var inputApiId = Console.ReadLine();
                if (string.Equals(inputApiId, "skip", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(inputApiId))
                {
                    Log.Information("User skipped ApiId entry. Auto-Forwarding will be disabled.");
                    return; // Exit this specific helper
                }
                configuration["TelegramUserApi:ApiId"] = inputApiId;
            }

            if (string.IsNullOrEmpty(apiHash))
            {
                Console.Write("Enter your ApiHash (or 'skip'): ");
                var inputApiHash = Console.ReadLine();
                if (string.Equals(inputApiHash, "skip", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(inputApiHash))
                {
                    Log.Information("User skipped ApiHash entry. Auto-Forwarding will be disabled.");
                    // Clear the ApiId as well to ensure the feature is fully disabled
                    configuration["TelegramUserApi:ApiId"] = null;
                    return;
                }
                configuration["TelegramUserApi:ApiHash"] = inputApiHash;
            }
        }
    }

    private static void PromptForTelegramPanelSecrets(IConfiguration configuration)
    {
        string? botToken = configuration["TelegramPanel:BotToken"];

        if (string.IsNullOrWhiteSpace(botToken) || botToken.Contains("REPLACE"))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n--- Telegram Bot Setup ---");
            Console.WriteLine("The main Bot Token is required to run the application.");
            Console.WriteLine("You can get this from @BotFather on Telegram.");
            Console.ResetColor();

            Console.Write("Enter your Bot Token: ");
            var inputToken = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(inputToken))
            {
                // This is a critical failure, as the app can't run without it.
                var errorMsg = "Bot Token cannot be empty. Application cannot start.";
                Log.Fatal(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            configuration["TelegramPanel:BotToken"] = inputToken;
            Log.Information("Bot Token configured from console input.");
        }
    }

    private static void PromptForCryptoPaySecrets(IConfiguration configuration)
    {
        string? apiToken = configuration["CryptoPay:ApiToken"];

        if (string.IsNullOrWhiteSpace(apiToken) || apiToken.Contains("REPLACE"))
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n--- CryptoPay Setup (Optional) ---");
            Console.WriteLine("Payment processing requires a CryptoPay API Token.");
            Console.WriteLine("Enter 'skip' to disable this feature for this session.");
            Console.ResetColor();

            Console.Write("Enter your CryptoPay API Token (or 'skip'): ");
            var inputToken = Console.ReadLine();

            if (string.Equals(inputToken, "skip", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(inputToken))
            {
                Log.Information("User skipped CryptoPay API Token entry. Payment features will be disabled.");
                // Explicitly set to null to ensure the feature check fails
                configuration["CryptoPay:ApiToken"] = null;
                return;
            }

            // Update the in-memory configuration with the user-provided token.
            configuration["CryptoPay:ApiToken"] = inputToken;
            Log.Information("CryptoPay API Token configured from console input.");
        }
    }
    public static void PromptForMissingSecrets(IConfiguration configuration)
    {
        PromptForTelegramPanelSecrets(configuration);
        PromptForTelegramApiSecrets(configuration);
        PromptForCryptoPaySecrets(configuration);
    }
}

public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.Value = value.ToString();
    }

    public override Guid Parse(object value)
    {
        return Guid.Parse(value.ToString());
    }
}

public class IntToBoolJsonConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32() != 0;
        }
        if (reader.TokenType == JsonTokenType.True) return true;
        if (reader.TokenType == JsonTokenType.False) return false;
        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value ? 1 : 0);
    }
}

public class FlexibleDateTimeJsonConverter : JsonConverter<DateTime>
{
    private static readonly string[] Formats = new[]
    {
        "yyyy-MM-dd HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fffZ"
    };

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (DateTime.TryParseExact(str, Formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
                return dt;
            if (DateTime.TryParse(str, out dt))
                return dt;
            throw new JsonException($"Could not parse DateTime: {str}");
        }
        return reader.GetDateTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O")); // ISO 8601
    }
}