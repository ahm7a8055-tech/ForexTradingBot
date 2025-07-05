using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Application.Tests
{
    // Placeholder setting classes to mimic actual application settings
    public class DatabaseSettings
    {
        public string? ConnectionString { get; set; }
        public int TimeoutSeconds { get; set; }
    }

    public class RedisSettings
    {
        public string? Configuration { get; set; }
        public bool AbortOnConnectFail { get; set; } = true; // Default value
    }

    public class ExternalApiServiceSettings
    {
        public string? ApiKey { get; set; }
        public string? BaseUrl { get; set; }
    }

    public class ConfigurationTests
    {
        private IConfigurationRoot _config;

        public ConfigurationTests()
        {
            // Build a configuration in memory for testing
            // This simulates settings that might come from appsettings.json or environment variables
            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"Database:ConnectionString", "Server=test_server;Database=test_db;User ID=test_user;Password=test_password"},
                    {"Database:TimeoutSeconds", "30"},
                    {"Redis:Configuration", "test_redis:6379"},
                    {"Redis:AbortOnConnectFail", "false"}, // Override default
                    {"ExternalApi:ApiKey", "test_api_key_123"},
                    {"ExternalApi:BaseUrl", "https://api.testservice.com"}
                })
                .Build();
        }

        [Fact]
        public void DatabaseSettings_Should_Load_Correctly()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(_config); // Register the built configuration
            services.Configure<DatabaseSettings>(_config.GetSection("Database"));
            var serviceProvider = services.BuildServiceProvider();

            // Act
            var settings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

            // Assert
            Assert.NotNull(settings);
            Assert.Equal("Server=test_server;Database=test_db;User ID=test_user;Password=test_password", settings.ConnectionString);
            Assert.Equal(30, settings.TimeoutSeconds);
        }

        [Fact]
        public void RedisSettings_Should_Load_Correctly_With_Default_Override()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(_config);
            services.Configure<RedisSettings>(_config.GetSection("Redis"));
            var serviceProvider = services.BuildServiceProvider();

            // Act
            var settings = serviceProvider.GetRequiredService<IOptions<RedisSettings>>().Value;

            // Assert
            Assert.NotNull(settings);
            Assert.Equal("test_redis:6379", settings.Configuration);
            Assert.False(settings.AbortOnConnectFail); // Check that the default was overridden
        }

        [Fact]
        public void ExternalApiServiceSettings_Should_Load_Correctly()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(_config);
            services.Configure<ExternalApiServiceSettings>(_config.GetSection("ExternalApi"));
            var serviceProvider = services.BuildServiceProvider();

            // Act
            var settings = serviceProvider.GetRequiredService<IOptions<ExternalApiServiceSettings>>().Value;

            // Assert
            Assert.NotNull(settings);
            Assert.Equal("test_api_key_123", settings.ApiKey);
            Assert.Equal("https://api.testservice.com", settings.BaseUrl);
        }

        [Fact]
        public void Missing_Configuration_Section_Should_Result_In_Default_Or_Null_Settings()
        {
            // Arrange
            var config = new ConfigurationBuilder() // Build a config with a missing section
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"SomeOtherSection:Value", "something"}
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.Configure<ExternalApiServiceSettings>(config.GetSection("MissingExternalApi"));
            var serviceProvider = services.BuildServiceProvider();

            // Act
            var settings = serviceProvider.GetRequiredService<IOptions<ExternalApiServiceSettings>>().Value;

            // Assert
            Assert.NotNull(settings); // Options<T> itself is never null
            Assert.Null(settings.ApiKey); // Properties should be null if not found and nullable
            Assert.Null(settings.BaseUrl);
        }

        [Fact]
        public void Configuration_Value_Can_Be_Read_Directly()
        {
            // Arrange & Act
            string? apiKey = _config["ExternalApi:ApiKey"];
            string? timeoutStr = _config.GetValue<string>("Database:TimeoutSeconds");
            int? timeoutInt = _config.GetValue<int>("Database:TimeoutSeconds");

            // Assert
            Assert.Equal("test_api_key_123", apiKey);
            Assert.Equal("30", timeoutStr);
            Assert.Equal(30, timeoutInt);
        }
    }
}
