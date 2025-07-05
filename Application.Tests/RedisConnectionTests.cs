using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis; // For ConfigurationOptions if needed for more complex scenarios

namespace Application.Tests
{
    // Using RedisSettings from ConfigurationTests.cs for consistency
    // public class RedisSettings
    // {
    //     public string? Configuration { get; set; }
    //     public bool AbortOnConnectFail { get; set; } = true;
    // }

    // Example: A service that needs Redis configuration
    public interface IRedisConfigurationService
    {
        string GetRedisConfigurationString();
        ConfigurationOptions GetRedisConfigurationOptions(); // For more complex setups
    }

    public class RedisConfigurationService : IRedisConfigurationService
    {
        private readonly IOptions<RedisSettings> _redisSettings;

        public RedisConfigurationService(IOptions<RedisSettings> redisSettings)
        {
            _redisSettings = redisSettings;
        }

        public string GetRedisConfigurationString()
        {
            return _redisSettings.Value?.Configuration ?? "localhost"; // Default to localhost if not configured
        }

        public ConfigurationOptions GetRedisConfigurationOptions()
        {
            var settingsValue = _redisSettings.Value;
            if (settingsValue == null || string.IsNullOrWhiteSpace(settingsValue.Configuration))
            {
                // Fallback to a default configuration or throw
                var defaultOptions = ConfigurationOptions.Parse("localhost");
                defaultOptions.AbortOnConnectFail = true; // Default behavior
                return defaultOptions;
            }

            var options = ConfigurationOptions.Parse(settingsValue.Configuration);
            options.AbortOnConnectFail = settingsValue.AbortOnConnectFail;
            // Add other specific options if RedisSettings becomes more complex
            // e.g., options.Ssl = settingsValue.UseSsl;
            // options.Password = settingsValue.Password;
            return options;
        }
    }

    public class RedisConnectionTests
    {
        private ServiceProvider _serviceProvider;
        private IConfigurationRoot _config;

        private void SetupServices(Dictionary<string, string?>? configData = null)
        {
            var defaultConfigData = new Dictionary<string, string?>
            {
                {"Redis:Configuration", "redis.example.com:6379,password=testpass,ssl=True"},
                {"Redis:AbortOnConnectFail", "false"}
            };

            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(configData ?? defaultConfigData)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(_config);
            services.Configure<RedisSettings>(_config.GetSection("Redis"));
            services.AddTransient<IRedisConfigurationService, RedisConfigurationService>();

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public void GetRedisConfigurationString_Returns_Correct_String_From_Settings()
        {
            // Arrange
            SetupServices();
            var redisConfigService = _serviceProvider.GetRequiredService<IRedisConfigurationService>();
            var expectedConfigString = "redis.example.com:6379,password=testpass,ssl=True";

            // Act
            string actualConfigString = redisConfigService.GetRedisConfigurationString();

            // Assert
            Assert.Equal(expectedConfigString, actualConfigString);
        }

        [Fact]
        public void GetRedisConfigurationString_Returns_Default_If_Configuration_Missing()
        {
            // Arrange
            SetupServices(new Dictionary<string, string?>()); // Empty config
            var redisConfigService = _serviceProvider.GetRequiredService<IRedisConfigurationService>();
            var expectedDefault = "localhost";

            // Act
            string actualConfigString = redisConfigService.GetRedisConfigurationString();

            // Assert
            Assert.Equal(expectedDefault, actualConfigString);
        }

        [Fact]
        public void GetRedisConfigurationOptions_Parses_String_And_Applies_Settings_Correctly()
        {
            // Arrange
            SetupServices(); // Uses default config data with AbortOnConnectFail = false
            var redisConfigService = _serviceProvider.GetRequiredService<IRedisConfigurationService>();

            // Act
            var options = redisConfigService.GetRedisConfigurationOptions();

            // Assert
            Assert.NotNull(options);
            Assert.False(options.AbortOnConnectFail); // Overridden from settings
            Assert.True(options.Ssl);
            Assert.Equal("testpass", options.Password);
            Assert.Contains(options.EndPoints, ep => ep.ToString() == "redis.example.com:6379");
        }

        [Fact]
        public void GetRedisConfigurationOptions_Handles_Complex_Configuration_String()
        {
            // Arrange
            var complexConfig = new Dictionary<string, string?>
            {
                {"Redis:Configuration", "server1:6379,server2:6380,allowAdmin=true,ssl=false,connectTimeout=5000,syncTimeout=5000"},
                {"Redis:AbortOnConnectFail", "true"}
            };
            SetupServices(complexConfig);
            var redisConfigService = _serviceProvider.GetRequiredService<IRedisConfigurationService>();

            // Act
            var options = redisConfigService.GetRedisConfigurationOptions();

            // Assert
            Assert.True(options.AbortOnConnectFail);
            Assert.False(options.Ssl);
            Assert.True(options.AllowAdmin);
            Assert.Equal(5000, options.ConnectTimeout);
            Assert.Equal(5000, options.SyncTimeout);
            Assert.Equal(2, options.EndPoints.Count); // server1, server2
        }

        [Fact]
        public void GetRedisConfigurationOptions_Returns_Default_Options_If_Configuration_String_Is_Null_Or_Empty()
        {
            // Arrange
            var emptyConfig = new Dictionary<string, string?> { { "Redis:Configuration", "" } };
            SetupServices(emptyConfig);
            var redisConfigService = _serviceProvider.GetRequiredService<IRedisConfigurationService>();

            // Act
            var options = redisConfigService.GetRedisConfigurationOptions();

            // Assert
            Assert.NotNull(options);
            Assert.True(options.AbortOnConnectFail); // Default from ConfigurationOptions
            Assert.Contains(options.EndPoints, ep => ep.ToString() == "localhost:6379" || ep.ToString() == "127.0.0.1:6379"); // Default endpoint
        }
    }
}
