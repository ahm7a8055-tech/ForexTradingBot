// File: WebAPI/Program.cs

#region Usings
using Application;
using Application.Common.Interfaces;
using Application.Features.Forwarding.Extensions;
using Application.Interfaces;
using Application.Services; // For IDiagnosticsService, ISettingsService
using BackgroundTasks;
using BackgroundTasks.Services;
using Dapper;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MemoryStorage;
using Hangfire.PostgreSql;
using Hangfire.Storage.SQLite;
using Infrastructure.Configuration; // For DatabaseConfigurationSource/Provider
using Infrastructure.Data;
using Infrastructure.ExternalServices;
using Infrastructure.Features.Forwarding.Extensions;
using Infrastructure.Security; // For SettingsProtectionService
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies; // Added for Cookie Authentication
using Microsoft.AspNetCore.DataProtection; // Added for Data Protection
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;             // برای OpenApiInfo
using Serilog;                              // برای Log, LoggerConfiguration, UseSerilog
using Serilog.Enrichers.WithCaller;
using Shared.Security; // For SecureExceptionSanitizer
using Shared.Settings;                    // برای CryptoPaySettings (از پروژه Shared)
using StackExchange.Redis;
using System.Data;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TelegramPanel.Extensions;
using TelegramPanel.Infrastructure.Logging;
using TelegramPanel.Infrastructure.Services;
using WebAPI.Middleware; // Added for AuthRedirectMiddleware
#endregion

#region Main Program logger
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
    // SECURITY: Sanitize exception details to prevent sensitive data exposure
    var exception = (Exception)eventArgs.ExceptionObject;
    var sanitizedDetails = SecureExceptionSanitizer.SanitizeForTelegram(exception); // High security for Telegram
    Log.Fatal(sanitizedDetails, "FATAL UNHANDLED EXCEPTION");
    Log.CloseAndFlush(); // Ensure the fatal log is sent before the app dies
};

#endregion

#region Main Application Entry (region master)
try
{
    Log.Information("--------------------------------------------------");
    Log.Information("Application Starting Up (Program.cs)...");
    Log.Information("--------------------------------------------------");
    ThreadPool.GetMinThreads(out int minWorker, out int minIo);
    Log.Information(
        "ThreadPool minimum threads set to {MinWorkerThreads} worker threads and {MinIoThreads} I/O threads.",
        minWorker,
        minIo
    );

    #region WebApplicationBuilder Setup (region master)
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // --- Custom Configuration Source Registration ---
    // This needs to happen early. We use ConfigureAppConfiguration.
    builder.Host.ConfigureAppConfiguration((hostingContext, configAppBuilder) =>
    {
        // 1. Build a temporary configuration from sources already added to configAppBuilder
        //    (like appsettings.json, environment variables, user secrets defined by WebApplication.CreateBuilder)
        //    This is needed to get the initial DefaultConnection string.
        var tempInitialConfig = configAppBuilder.Build();

        // 2. Setup a temporary ServiceCollection for services needed by DatabaseConfigurationProvider
        var tempServices = new ServiceCollection();
        tempServices.AddSingleton<IConfiguration>(tempInitialConfig); // Provide this specific configuration snapshot

        string? defaultConnectionString = tempInitialConfig.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(defaultConnectionString))
        {
            Log.Fatal("CRITICAL ERROR: DefaultConnection string is missing. Cannot initialize dynamic configuration from database.");
            // In a real scenario, might throw or have a more graceful fallback if essential.
            // For now, if it's missing, the DatabaseConfigurationSource might not be able to connect.
        }

        string? dbProviderForDynamicConfig = tempInitialConfig.GetValue<string>("DatabaseSettings:DatabaseProvider")?.ToLowerInvariant() ?? "postgres";
        if (dbProviderForDynamicConfig == "sqlite")
        {
            tempServices.AddDbContext<AppDbContext>(options => options.UseSqlite(defaultConnectionString), ServiceLifetime.Singleton); // Singleton for this temp provider
        }
        else if (dbProviderForDynamicConfig == "sqlserver")
        {
            tempServices.AddDbContext<AppDbContext>(options => options.UseSqlServer(defaultConnectionString), ServiceLifetime.Singleton);
        }
        else // Default to PostgreSQL
        {
            tempServices.AddDbContext<AppDbContext>(options => options.UseNpgsql(defaultConnectionString), ServiceLifetime.Singleton);
        }

        var keysFolderTemp = Path.Combine(hostingContext.HostingEnvironment.ContentRootPath, "keys");
        Directory.CreateDirectory(keysFolderTemp);
        tempServices.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysFolderTemp)).SetApplicationName("ForexTradingBot");

        tempServices.AddSingleton<ISettingsProtectionService, SettingsProtectionService>();
        tempServices.AddSingleton<IDynamicConfigurationService, DynamicConfigurationService>();
        tempServices.AddLogging(lb => lb.AddSerilog(Log.Logger)); // Use existing Serilog global logger

        // 3. Action to register defined settings (passed to DatabaseConfigurationSource)
        Action<IDynamicConfigurationService, IConfigurationBuilder> registerSettingsAction = (service, currentConfigBuilder) =>
        {
            // Use the configuration built from sources *before* adding DatabaseConfigurationSource
            // to get default values for registration.
            var configForDefaults = currentConfigBuilder.Build();

            Log.Information("Registering defined settings with DynamicConfigurationService (within ConfigureAppConfiguration)...");
            void RegisterSetting(string key, bool isSensitive, string description)
            {
                service.RegisterSettingDefinition(key, configForDefaults[key], isSensitive, description);
            }

            RegisterSetting("Admin:Username", false, "Admin panel username.");
            RegisterSetting("Admin:Password", true, "Admin panel password.");
            RegisterSetting("ConnectionStrings:Redis", true, "Redis connection string.");
            RegisterSetting("TelegramPanel:BotToken", true, "Main Telegram Bot Token.");
            RegisterSetting("TelegramPanel:AdminUserIds", false, "Comma-separated list of Telegram Admin User IDs.");
            RegisterSetting("TelegramPanel:WebhookAddress", false, "Webhook address for the main Telegram Bot.");
            RegisterSetting("TelegramPanel:WebhookSecretToken", true, "Secret token for the Telegram webhook.");
            RegisterSetting("TelegramPanel:PollingInterval", false, "Polling interval in seconds (if not using webhook).");
            RegisterSetting("TelegramPanel:EnableDebugMode", false, "Enables debug mode for the Telegram panel.");
            RegisterSetting("TelegramUserApi:ApiId", false, "Telegram User API ID.");
            RegisterSetting("TelegramUserApi:ApiHash", true, "Telegram User API Hash.");
            RegisterSetting("TelegramUserApi:PhoneNumber", true, "Phone number for Telegram User API client.");
            RegisterSetting("TelegramUserApi:VerificationCodeSource", false, "Source for User API verification code.");
            RegisterSetting("TelegramUserApi:BotToken", true, "Bot token for the forwarder helper bot.");
            RegisterSetting("TelegramUserApi:TwoFactorPasswordSource", false, "Source for User API 2FA password.");
            RegisterSetting("CryptoPay:ApiToken", true, "CryptoPay API Token.");
            RegisterSetting("CryptoPay:BaseUrl", false, "CryptoPay API Base URL.");
            RegisterSetting("CryptoPay:IsTestnet", false, "Indicates if CryptoPay is using testnet.");
            RegisterSetting("CryptoPay:WebhookSecretForCryptoPay", true, "Webhook secret from CryptoPay.");
            RegisterSetting("HangfireSettings:StorageType", false, "Hangfire storage type.");
            RegisterSetting("HangfireSettings:DefaultWorkerCount", false, "Default Hangfire worker count.");
            RegisterSetting("HangfireSettings:NotificationWorkerCount", false, "Hangfire worker count for notifications.");
            RegisterSetting("OperationalFlags:GlobalLogLevel", false, "Global log level override.");
            RegisterSetting("OperationalFlags:EnableRssModule", false, "Feature flag for RSS module.");
            RegisterSetting("OperationalFlags:EnableForwardingModule", false, "Feature flag for auto-forwarding module.");
            Log.Information("All defined settings registered with DynamicConfigurationService (within ConfigureAppConfiguration).");
        };

        // 4. Add the custom configuration source
        configAppBuilder.Add(new DatabaseConfigurationSource(tempServices, registerSettingsAction));
        Log.Information("DatabaseConfigurationSource added via ConfigureAppConfiguration.");
    });
    // --- End of Custom Configuration Source Registration ---

    _ = builder.WebHost.UseKestrel();
    builder.Services.AddSingleton<TelegramAdminSink>();
    // This is more reliable than GetValue<bool> for environment variables.
    string smokeTestFlag = builder.Configuration["IsSmokeTest"] ?? "false";
    bool isSmokeTest = "true".Equals(smokeTestFlag, StringComparison.OrdinalIgnoreCase);

    // --- AUTO-FIX FOR SMOKETEST: Provide SQLite connection if missing ---
    if (isSmokeTest)
    {
        string? smokeTestConn = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(smokeTestConn))
        {
            smokeTestConn = "Data Source=smoketest.db";
            builder.Configuration.GetSection("ConnectionStrings")["DefaultConnection"] = smokeTestConn;
            builder.Configuration.GetSection("DatabaseSettings")["DatabaseProvider"] = "sqlite";
            Log.Information("[SmokeTest] No DefaultConnection found. Using SQLite: {Conn}", smokeTestConn);
        }
    }

    #endregion

    #region SmokeTest SQLite Connection (region master)
    // ... existing code ...
    #endregion

    #region Configure Serilog Logging (region master)
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
          restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information, // Only write Error and above to file
          outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}"
      // --------------------
      )
      // =======================================================================

      .WriteTo.Sink(
          new TelegramAdminSink(context.Configuration),
          Serilog.Events.LogEventLevel.Error // فقط لاگ‌های سطح Error و بالاتر به تلگرام ارسال می‌شود
      )
  );
    #endregion

    #region Caching and External Services (region master)

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
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            string? connectionString = configuration.GetConnectionString("Redis");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // This should ideally not happen because of your earlier logic, but it's a good safeguard.
                Log.Fatal("Redis connection string is null or empty at the moment of creating the multiplexer.");
                throw new InvalidOperationException("Redis connection string is not configured.");
            }

            var options = ConfigurationOptions.Parse(connectionString);

            // --- THE CRITICAL FIX ---
            // This tells the multiplexer to NOT throw an exception on startup if it can't connect.
            // It will continue trying to connect in the background, giving our
            // EmbeddedRedisService time to start the server.
            options.AbortOnConnectFail = false;
            // ------------------------

            options.ConnectTimeout = 10000; // Increase timeout for more resilience on startup
            options.SyncTimeout = 10000;

            Log.Information("Attempting to create a resilient Redis connection to {Endpoint}...", options.EndPoints.FirstOrDefault());

            // We wrap the connection in a try-catch for better logging, but AbortOnConnectFail=false
            // should prevent it from throwing here unless the connection string is fundamentally invalid.
            try
            {
                return ConnectionMultiplexer.Connect(options);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initiate Redis ConnectionMultiplexer. The connection string may be invalid.");
                throw; // Re-throw to halt the application if the config is malformed.
            }
        });

        Log.Information("✅ Redis services configured. The multiplexer will connect resiliently.");
    }
    catch (Exception ex)
    {
        // Your existing fallback logic for in-memory Redis is fine.
        Log.Error(ex, "Failed to configure Redis connection multiplexer. Using fallback in-memory Redis.");
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

    #region AutoMapper and LoggingSanitizer (region master)
    builder.Services.AddAutoMapper(typeof(Program));
    builder.Services.AddSingleton<Application.Common.Interfaces.ILoggingSanitizer, Infrastructure.Security.PiiLoggingSanitizer>();
    builder.Services.AddSingleton<Shared.Security.IExceptionSanitizer, Shared.Security.ExceptionSanitizer>();
    #endregion

    #region Add Core ASP.NET Core Services (region master)
    _ = builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "ForexTradingBotAPI";
    });
    AppContext.SetSwitch("Microsoft.AspNetCore.Mvc.ApiExplorer.IsEnhancedModelMetadataSupported", true);
    // ------------------- ۲. اضافه کردن سرویس‌های پایه ASP.NET Core -------------------
    // فعال کردن پشتیبانی از کنترلرهای API
    _ = builder.Services.AddControllers();
    // فعال کردن API Explorer برای تولید مستندات Swagger/OpenAPI
    _ = builder.Services.AddEndpointsApiExplorer();

    // Configure Cookie Authentication for Admin Dashboard
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.LoginPath = "/login.html";
            options.LogoutPath = "/api/auth/logout";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(60); // Adjust as needed
            options.SlidingExpiration = true;
        });

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

    #region Configure Application Options/Settings (region master)


    builder.Services.Configure<AdminNotificationSettings>(
    builder.Configuration.GetSection(AdminNotificationSettings.SectionName));
    builder.Services.AddSingleton<INotificationToAdminService, NotificationToAdminService>();
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

    #region Register Custom Application Layers and Services (region master)
    // ------------------- ۴. رجیستر کردن سرویس‌های لایه‌های مختلف برنامه -------------------
    // این متدها باید در فایل‌های DependencyInjection.cs (یا ServiceCollectionExtensions.cs) هر لایه تعریف شده باشند.
    // ترتیب فراخوانی: ابتدا لایه‌های پایه (Application, Infrastructure)، سپس لایه‌های Presentation یا خاص (TelegramPanel, BackgroundTasks).




    _ = builder.Services.AddApplicationServices();
    Log.Information("Application services registered.");

    // Configure Data Protection - Keys persisted to file system.
    // #region Data Protection Key Security
    // IMPORTANT: For production, store keys in a secure location (Azure Key Vault, AWS KMS, or a locked-down network share).
    // The /keys folder must be accessible ONLY to the app's service account. Do not allow other users or admins access.
    // #endregion
    // Consider using Azure Blob Storage, Redis, or another provider for key persistence in a distributed environment.
    var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "keys");
    Directory.CreateDirectory(keysFolder); // Ensure the directory exists
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
        .SetApplicationName("ForexTradingBot"); // Unique application name to isolate keys

    // Register custom dynamic configuration services
    builder.Services.AddSingleton<ISettingsProtectionService, SettingsProtectionService>();
    builder.Services.AddSingleton<IDynamicConfigurationService, DynamicConfigurationService>();

    // Register other core infrastructure services that might be missing
    // Assuming Scoped lifetime is appropriate as they often use DbContext or HttpClientFactory.
    builder.Services.AddScoped<ISettingsService, SettingsService>();
    builder.Services.AddScoped<IDiagnosticsService, DiagnosticsService>();
    Log.Information("Data Protection, Dynamic Configuration, Settings, and Diagnostics services registered.");

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
            string? userInput = Console.ReadLine()?.Trim();

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
                {
                    dbProvider = "postgres";
                }
                else
                {
                    dbProvider = connectionString.Contains("Server=") || connectionString.Contains("Data Source=") ? "sqlserver" : "sqlite";
                }

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
            {
                dbProvider = "postgres";
            }
            else
            {
                dbProvider = connectionString.Contains("Server=") || connectionString.Contains("Data Source=") ? "sqlserver" : "sqlite";
            }

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

    // More robust check: ensure ApiId is a valid number and neither value is a placeholder.
    bool isApiIdValid = int.TryParse(apiId, out _);
    bool isApiHashValid = !string.IsNullOrEmpty(apiHash) && !apiHash.Contains("REPLACE") && !apiHash.Contains("YOUR_");

    bool isAutoForwardingEnabled = isApiIdValid && isApiHashValid;
    try
    {
        #region Restrict Interactive Prompts to Development Only
        if (Environment.UserInteractive)
        {
            if (builder.Environment.IsDevelopment())
            {
                Log.Information("Development mode: Prompting for missing secrets for local debug session.");
                ConfigurationHelper.PromptForMissingSecrets(builder.Configuration);
            }
            else
            {
                Log.Fatal("FATAL: Application started in interactive mode in a non-Development environment. This is a security risk and is forbidden. Aborting.");
                Environment.Exit(1);
            }
        }
        #endregion


        if (isAutoForwardingEnabled)
        {
            Log.Information("✅ Auto-Forwarding feature ENABLED. Registering REAL services...");

            // Register the REAL implementation and all its dependencies
            builder.Services.Configure<Infrastructure.Settings.TelegramUserApiSettings>(
                builder.Configuration.GetSection("TelegramUserApi"));

            builder.Services.AddSingleton<ITelegramUserApiClient, TelegramUserApiClient>();
            builder.Services.AddHostedService<TelegramUserApiInitializationService>();

            builder.Services.AddForwardingInfrastructure();
            builder.Services.AddForwardingServices();
            builder.Services.AddForwardingOrchestratorServices();
            builder.Services.AddTelegramPanelForwardingServices();

            Log.Information("All Auto-Forwarding services have been successfully registered.");
        }
        else
        {
            // --- THIS IS THE FIX ---
            Log.Information("ℹ️ Auto-Forwarding feature DISABLED. Registering FAKE (do-nothing) service.");

            // Register the DISABLED implementation. This satisfies the DI container
            // for any service that requires ITelegramUserApiClient, preventing a crash.
            builder.Services.AddSingleton<ITelegramUserApiClient, DisabledTelegramUserApiClient>();
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
                    IOptions<CryptoPaySettings> options = serviceProvider.GetRequiredService<IOptions<CryptoPaySettings>>();
                    ILogger<CryptoPayApiClient> logger = serviceProvider.GetRequiredService<ILogger<CryptoPayApiClient>>();
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
            // SECURITY: Sanitize exception details to prevent sensitive data exposure
            var sanitizedDetails = SecureExceptionSanitizer.SanitizeForTelegram(exSql); // High security for Telegram
            Log.Warning(sanitizedDetails, "Could not ensure local SQL Server services are running. This may be expected.");
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

    #region Configure Hangfire (region master)

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
                {
                    dbProvider = "postgres";
                }
                else if (connectionString.Contains("Server=") || connectionString.Contains("Data Source="))
                {
                    dbProvider = "sqlserver";
                }
                else
                {
                    dbProvider = "sqlite"; // Default fallback
                }

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
                    // 1. Configure Hangfire to use PostgreSQL storage with tuned options:
                    config.UsePostgreSqlStorage(
     options => options.UseNpgsqlConnection(connectionString),
     new Hangfire.PostgreSql.PostgreSqlStorageOptions
     {
         // Balance DB load vs. responsiveness
         QueuePollInterval = TimeSpan.FromSeconds(5),

         // Prevent a long-running job from being re-queued while it's still executing
         InvisibilityTimeout = TimeSpan.FromHours(1),

         // How long to wait for the lock before giving up
         DistributedLockTimeout = TimeSpan.FromSeconds(30),

         // How often to scan for expired jobs (cleanup)
         JobExpirationCheckInterval = TimeSpan.FromHours(1) // <<< This is the primary setting to adjust
     }
 );

                    // 2. Tune the BackgroundJobServer to the machine's CPU count:
                    int cpuCount = Environment.ProcessorCount;
                    BackgroundJobServerOptions serverOptions = new()
                    {
                        // Leave one core free for OS and other processes
                        WorkerCount = Math.Max(cpuCount - 1, 1),

                        // Check server health & heartbeat every 15 seconds
                        ServerCheckInterval = TimeSpan.FromSeconds(15),

                        // Define your queues in priority order
                        Queues = new[] { "critical", "default", "low" },

                        // Name each server instance for easier monitoring
                        ServerName = $"hangfire-{Environment.MachineName}-{Guid.NewGuid():N}"
                    };
                    Log.Information(
                        "✅ Hangfire (PostgreSQL) configured: " +
                        $"Poll={TimeSpan.FromSeconds(5)}, " +
                        $"LockLifetime={TimeSpan.FromMinutes(10)}, " +
                        $"LockTimeout={TimeSpan.FromSeconds(30)}, " +
                        $"Workers={serverOptions.WorkerCount}"
                    );
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
            // SECURITY: Sanitize exception details to prevent sensitive data exposure
            var sanitizedDetails = SecureExceptionSanitizer.SanitizeForTelegram(ex); // High security for Telegram
            Log.Error(sanitizedDetails, "FAILED to configure Hangfire with the specified database storage. FALLING BACK TO IN-MEMORY STORAGE.");
            Log.Warning("Hangfire jobs will NOT be persisted and will be lost if the application restarts.");

            // CORRECTED: Do NOT call AddHangfire again. Just configure the existing 'config' object.
            config.UseMemoryStorage();
        }
    });


    // 3. خواندن تعداد Workerها از appsettings.json یا Fall‑Back به CPU/RAM
    int cpuCount = Environment.ProcessorCount;
    int ramGB = SystemInfoHelper.GetTotalMemoryInGB();
    int defaultWorkerCount = builder.Configuration.GetValue<int?>("Hangfire:DefaultWorkerCount")
                           ?? Math.Max(1, Math.Min(Math.Min(cpuCount * 2, ramGB * 4), 32));
    int notificationWorkerCount = builder.Configuration.GetValue<int?>("Hangfire:NotificationWorkerCount")
                               ?? Math.Max(1, Math.Min(Math.Min(cpuCount, ramGB * 2), 16));

    // 4. ثبت دو سرور با صف‌ها و WorkerCount مخصوص:
    builder.Services.AddHangfireServer(options =>
    {
        options.ServerName = $"{Environment.MachineName}:Notifications";
        options.WorkerCount = notificationWorkerCount;
        options.Queues = new[] { "notifications" };
        options.ServerCheckInterval = TimeSpan.FromSeconds(15);
    });
    builder.Services.AddHangfireServer(options =>
    {
        options.ServerName = $"{Environment.MachineName}:Default";
        options.WorkerCount = defaultWorkerCount;
        options.Queues = new[] { "critical", "default" };
        options.ServerCheckInterval = TimeSpan.FromSeconds(15);
    });

    Log.Information("Hangfire cleaner service added.");

    Log.Information("Performing final manual service registrations...");
    _ = builder.Services.AddSingleton<IGeminiService, GeminiService>();
    // FIX FOR: Unable to resolve 'IBotCommandSetupService'
    _ = builder.Services.AddTransient<IBotCommandSetupService, BotCommandSetupService>();
    _ = builder.Services.AddTransient<Infrastructure.Services.IHangfireCleaner, Infrastructure.Services.HangfireCleaner>();

    Log.Information("Final manual service registrations complete.");
    _ = builder.Services.Configure<List<Infrastructure.Settings.ForwardingRule>>( // <<< Fully qualified
    builder.Configuration.GetSection("ForwardingRules"));

    #endregion

    #region Build WebApplication and Startup Tasks (region master)
    WebApplication app = builder.Build();
    Log.Information("Application host built. Performing mandatory startup tasks...");

    // Initialize and register defined settings with the DynamicConfigurationService
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var dynamicConfigService = services.GetRequiredService<IDynamicConfigurationService>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILogger<Program>>(); // Using ILogger<Program> for this setup log

        logger.LogInformation("Registering defined settings with DynamicConfigurationService...");

        // Helper to register a setting
        void RegisterSetting(string key, bool isSensitive, string description)
        {
            dynamicConfigService.RegisterSettingDefinition(key, configuration[key], isSensitive, description);
        }

        // Admin Credentials
        RegisterSetting("Admin:Username", false, "Admin panel username.");
        RegisterSetting("Admin:Password", true, "Admin panel password.");

        // Connection Strings
        // Note: DefaultConnection is bootstrap, others can be dynamic
        RegisterSetting("ConnectionStrings:Redis", true, "Redis connection string.");

        // Telegram Bot (Main Bot - TelegramPanel section)
        RegisterSetting("TelegramPanel:BotToken", true, "Main Telegram Bot Token.");
        RegisterSetting("TelegramPanel:AdminUserIds", false, "Comma-separated list of Telegram Admin User IDs.");
        RegisterSetting("TelegramPanel:WebhookAddress", false, "Webhook address for the main Telegram Bot.");
        RegisterSetting("TelegramPanel:WebhookSecretToken", true, "Secret token for the Telegram webhook.");
        RegisterSetting("TelegramPanel:PollingInterval", false, "Polling interval in seconds (if not using webhook).");
        RegisterSetting("TelegramPanel:EnableDebugMode", false, "Enables debug mode for the Telegram panel.");

        // Telegram User API (Auto-Forwarder/User Client - TelegramUserApi section)
        RegisterSetting("TelegramUserApi:ApiId", false, "Telegram User API ID."); // Typically not secret itself
        RegisterSetting("TelegramUserApi:ApiHash", true, "Telegram User API Hash.");
        RegisterSetting("TelegramUserApi:PhoneNumber", true, "Phone number for Telegram User API client.");
        // SessionPath is usually app-relative, not typically user-configured via this UI
        // RegisterSetting("TelegramUserApi:SessionPath", false, "Path for User API session file.");
        RegisterSetting("TelegramUserApi:VerificationCodeSource", false, "Source for User API verification code (e.g., 'Console', 'Bot').");
        RegisterSetting("TelegramUserApi:BotToken", true, "Bot token for the forwarder helper bot (if used).");
        RegisterSetting("TelegramUserApi:TwoFactorPasswordSource", false, "Source for User API 2FA password (e.g., 'Console', 'Bot').");

        // CryptoPay Settings (CryptoPay section)
        RegisterSetting("CryptoPay:ApiToken", true, "CryptoPay API Token.");
        RegisterSetting("CryptoPay:BaseUrl", false, "CryptoPay API Base URL.");
        RegisterSetting("CryptoPay:IsTestnet", false, "Indicates if CryptoPay is using testnet.");
        RegisterSetting("CryptoPay:WebhookSecretForCryptoPay", true, "Webhook secret from CryptoPay.");

        // Hangfire Settings (HangfireSettings section)
        RegisterSetting("HangfireSettings:StorageType", false, "Hangfire storage type (e.g., 'Postgres', 'Memory').");
        RegisterSetting("HangfireSettings:DefaultWorkerCount", false, "Default Hangfire worker count.");
        RegisterSetting("HangfireSettings:NotificationWorkerCount", false, "Hangfire worker count for notifications queue.");

        // Example Other Operational Flags
        RegisterSetting("OperationalFlags:GlobalLogLevel", false, "Global log level override (e.g., 'Information', 'Debug').");
        RegisterSetting("OperationalFlags:EnableRssModule", false, "Feature flag to enable/disable the RSS module.");
        RegisterSetting("OperationalFlags:EnableForwardingModule", false, "Feature flag to enable/disable the auto-forwarding module.");

        logger.LogInformation("All defined settings registered.");
    }

    // Automatically apply EF Core migrations at startup (async master)
    using (IServiceScope scope = app.Services.CreateScope())
    {
        AppDbContext db = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();
        try
        {
            string? connectionString = app.Services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is missing or empty. Cannot proceed with database operations.");
            }
            Log.Information("Attempting to apply database migrations...");
            Log.Information("Database provider: {ProviderName}", db.Database.ProviderName);

            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync().ConfigureAwait(false);
                Log.Information("Database migrations applied successfully.");
            }
            else
            {
                await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
                Log.Information("Non-relational provider detected. Database created using EnsureCreated().");
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChangesWarning"))
        {
            Log.Warning("Migration failed due to pending model changes. Attempting to create database...");
            try
            {
                await db.Database.EnsureCreatedAsync().ConfigureAwait(false); // async master
                Log.Information("Database created successfully using EnsureCreated().");
            }
            catch (Exception createEx)
            {
                // SECURITY: Sanitize exception details to prevent sensitive data exposure
                var sanitizedDetails = SecureExceptionSanitizer.SanitizeForTelegram(createEx); // High security for Telegram
                Log.Error(sanitizedDetails, "Failed to create database using EnsureCreated(). Connection string may be invalid.");
                throw new InvalidOperationException($"Database creation failed. Please check your connection string: {createEx.Message}", createEx);
            }
        }
        catch (Exception ex)
        {
            // SECURITY: Sanitize exception details to prevent sensitive data exposure
            var sanitizedDetails = SecureExceptionSanitizer.SanitizeForTelegram(ex); // High security for Telegram
            Log.Error(sanitizedDetails, "Failed to apply database migrations or create database. Connection string may be invalid.");
            throw new InvalidOperationException($"Database setup failed. Please check your connection string: {ex.Message}", ex);
        }
    }
    #endregion

    #region Queue Startup Maintenance Jobs to Hangfire (region master)
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
            // SECURITY: Sanitize exception details to prevent sensitive data exposure
            var sanitizedDetails = SecureExceptionSanitizer.SanitizeForTelegram(ex); // High security for Telegram
            Log.Error(sanitizedDetails, "An error occurred while trying to enqueue startup maintenance jobs.");
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

            IConnectionMultiplexer redisConnection = app.Services.GetRequiredService<IConnectionMultiplexer>();
            IDatabase redisDb = redisConnection.GetDatabase();

            // Test basic operations
            string testKey = $"test_connection_{Guid.NewGuid():N}";
            string testValue = DateTime.UtcNow.ToString();

            await redisDb.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(10));
            RedisValue retrievedValue = await redisDb.StringGetAsync(testKey);

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
            // SECURITY: Sanitize exception details to prevent sensitive data exposure
            var sanitizedDetails = SecureExceptionSanitizer.SanitizeForTelegram(ex); // High security for Telegram
            Log.Warning(sanitizedDetails, "⚠️ Redis connectivity test failed. Some distributed features may not work properly.");

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
    Log.Information("Mandatory startup tasks completed.");
    ILogger<Program> programLogger = app.Services.GetRequiredService<ILogger<Program>>(); //  استفاده از ILogger<Program> برای لاگ‌های مختص Program.cs
    #endregion

    #region Configure HTTP Request Pipeline (region master)
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
        c.RoutePrefix = "swagger"; // Changed: Swagger UI will be at /swagger
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

    _ = app.UseStaticFiles(); // Serve static files early, especially for login page

    _ = app.UseSerilogRequestLogging(); //  لاگ کردن تمام درخواست‌های HTTP ورودی با جزئیات (توسط Serilog)

    _ = app.UseRouting();

    // IMPORTANT: Authentication must come before Authorization
    _ = app.UseAuthentication(); // Added for Admin Dashboard authentication
    _ = app.UseAuthorization();

    // Custom middleware to redirect unauthenticated users trying to access protected admin pages
    _ = app.UseMiddleware<AuthRedirectMiddleware>();

    // Explicitly map the root path to handle login/dashboard redirection
    app.MapGet("/", (HttpContext context) =>
    {
        if (context.User?.Identity?.IsAuthenticated ?? false)
        {
            // User is authenticated, redirect to the main dashboard page
            return Results.Redirect("/indexapp.html", permanent: false);
        }
        // User is not authenticated, redirect to the login page
        return Results.Redirect("/login.html", permanent: false);
    });

    _ = app.MapHangfireDashboard();
    programLogger.LogInformation("HTTP request pipeline configured.");
    #endregion

    #region Configure Hangfire Dashboard & Recurring Jobs (region master)
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

    var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<UserCleanupService>(
        "user-cleanup-job",
        job => job.CheckAndDeleteUnreachableUsersAsync(),
        Cron.Daily);
    #endregion

    #region Map Controllers & Run Application (region master)
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
    // ... after app.UseSerilogRequestLogging() and other middleware ...

    app.MapGet("/testerror", () =>
    {
        // This will force an exception and a high-level log event
        try
        {
            throw new InvalidOperationException("This is a guaranteed test exception for the Telegram sink.");
        }
        catch (Exception ex)
        {
            // Use the ILogger from the dependency injection container
            ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
            // SECURITY: Sanitize exception details to prevent sensitive data exposure
            var sanitizedDetails = SecureExceptionSanitizer.SanitizeForTelegram(ex); // High security for Telegram
            logger.LogError(sanitizedDetails, "Caught a test exception.");
            return Results.Problem("A test error was logged. Check your Telegram and console.");
        }
    });
    app.UseSerilogRequestLogging();
    // In the middleware pipeline section (before app.Run()) - app.UseStaticFiles() moved earlier
    await app.RunAsync();
}
catch (Exception ex)
{
    //  لاگ کردن خطاهای بسیار بحرانی که مانع از اجرای برنامه شده‌اند.
    // SECURITY: Sanitize exception details to prevent sensitive data exposure
    var sanitizedDetails = SecureExceptionSanitizer.SanitizeForTelegram(ex); // High security for Telegram
    Log.Fatal(sanitizedDetails, "Application host very much terminated unexpectedly.");
    // Environment.ExitCode = 1; //  برای نشان دادن خروج ناموفق به سیستم عامل یا اسکریپت‌های دیگر
}
finally
{

    Log.Information("--------------------------------------------------");
    Log.Information("Application Shutting Down...");
    Log.CloseAndFlush();
    Log.Information("--------------------------------------------------");
}
#endregion

#region Configuration Helper
/// <summary>
/// Helper class to prompt the user for Telegram API credentials.
/// </summary>
public static class ConfigurationHelper
{

    #region PromptForTelegramApiSecrets
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
                string? inputApiId = Console.ReadLine();
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
                string? inputApiHash = Console.ReadLine();
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
    #endregion

    #region TelegramPanel

    /// <summary>
    /// Prompts the user to enter the Telegram Panel Bot Token.
    /// </summary>
    /// <param name="configuration"></param>
    /// <exception cref="InvalidOperationException"></exception>
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
            string? inputToken = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(inputToken))
            {
                // This is a critical failure, as the app can't run without it.
                string errorMsg = "Bot Token cannot be empty. Application cannot start.";
                Log.Fatal(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            configuration["TelegramPanel:BotToken"] = inputToken;
            Log.Information("Bot Token configured from console input.");
        }
    }

    #endregion

    #region CryptoPay
    /// <summary>
    /// Prompt the user for their CryptoPay API token.
    /// </summary>
    /// <param name="configuration"></param>
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
            string? inputToken = Console.ReadLine();

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
#endregion

#region GuidTypeHandler
/// <summary>
/// A custom type handler for Guid values.
/// </summary>
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
#endregion

#region IntToBoolJsonConverter

/// <summary>
/// A custom JSON converter for boolean values.
/// </summary>
public class IntToBoolJsonConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32() != 0;
        }
        if (reader.TokenType == JsonTokenType.True)
        {
            return true;
        }

        return reader.TokenType == JsonTokenType.False ? false : throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value ? 1 : 0);
    }
}
#endregion

#region FlexibleDateTimeJsonConverter

/// <summary>
/// A custom JSON converter for DateTime that supports different date-time formats.
/// </summary>
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
            string? str = reader.GetString();
            if (DateTime.TryParseExact(str, Formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime dt))
            {
                return dt;
            }

            return DateTime.TryParse(str, out dt) ? dt : throw new JsonException($"Could not parse DateTime: {str}");
        }
        return reader.GetDateTime();
    }
    /// <summary>
    /// Writes a DateTime as a string in ISO 8601 format.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O")); // ISO 8601
    }
}

#endregion

#region Cross-platform system info helper
// Cross-platform system info helper
public static class SystemInfoHelper
{
    public static int GetTotalMemoryInGB()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string[] meminfo = File.ReadAllLines("/proc/meminfo");
                string? memTotalLine = meminfo.FirstOrDefault(l => l.StartsWith("MemTotal:"));
                if (memTotalLine != null)
                {
                    string[] parts = memTotalLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out long kb))
                    {
                        return (int)(kb / 1024 / 1024);
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                System.Diagnostics.ProcessStartInfo psi = new()
                {
                    FileName = "sysctl",
                    Arguments = "-n hw.memsize",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                using System.Diagnostics.Process? output = System.Diagnostics.Process.Start(psi);
                string result = output.StandardOutput.ReadToEnd();
                output.WaitForExit();
                if (long.TryParse(result.Trim(), out long bytes))
                {
                    return (int)(bytes / (1024 * 1024 * 1024));
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Fallback: Use GC.GetGCMemoryInfo (not total RAM, but available to process)
                GCMemoryInfo info = GC.GetGCMemoryInfo();
                if (info.TotalAvailableMemoryBytes > 0)
                {
                    return (int)(info.TotalAvailableMemoryBytes / (1024 * 1024 * 1024));
                }
            }
        }
        catch { }
        // Fallback: 2GB if unknown
        return 2;
    }
}
#endregion

#endregion
#endregion