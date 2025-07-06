using Application.DTOs.Settings;
using Application.DTOs.Telegram; // Added for new DTOs
using Application.Interfaces;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;
using System; // Added for TimeSpan
using System.Threading.Tasks; // Added for Task
using System.Threading; // Added for CancellationToken

namespace Infrastructure.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly ICacheService _cacheService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SettingsService> _logger;

        private const string ForceJoinSettingsKey = "settings:force_join";
        private const string TelegramBotSettingsKey = "settings:telegram_bot";
        private const string TelegramClientSettingsKey = "settings:telegram_client";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(5);

        public SettingsService(ICacheService cacheService, IConfiguration configuration, ILogger<SettingsService> logger)
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ForceJoinSettingsDto> GetForceJoinSettingsAsync(CancellationToken cancellationToken = default)
        {
            // Corrected: GetAsync takes 1 argument
            ForceJoinSettingsDto? cachedSettings = await _cacheService.GetAsync<ForceJoinSettingsDto>(ForceJoinSettingsKey);
            if (cachedSettings is not null)
            {
                _logger.LogTrace("Force join settings retrieved from cache with key {CacheKey}.", ForceJoinSettingsKey);
                return cachedSettings;
            }

            _logger.LogInformation("Force join settings not found in cache with key {CacheKey}. Fetching from database.", ForceJoinSettingsKey);

            ForceJoinSettingsDto settings = await GetSettingFromDbAsync<ForceJoinSettingsDto>(ForceJoinSettingsKey, () => new ForceJoinSettingsDto { IsEnabled = false }, cancellationToken);

            // Corrected: SetAsync takes 3 arguments (key, value, expiry)
            await _cacheService.SetAsync(ForceJoinSettingsKey, settings, _defaultCacheDuration);
            _logger.LogInformation("Force join settings stored in cache with key {CacheKey}.", ForceJoinSettingsKey);

            return settings;
        }

        public async Task<TelegramBotSettingsDto> GetTelegramBotSettingsAsync(CancellationToken cancellationToken = default)
        {
            // Corrected: GetAsync takes 1 argument
            TelegramBotSettingsDto? cachedSettings = await _cacheService.GetAsync<TelegramBotSettingsDto>(TelegramBotSettingsKey);
            if (cachedSettings is not null)
            {
                _logger.LogTrace("Telegram bot settings retrieved from cache with key {CacheKey}.", TelegramBotSettingsKey);
                return cachedSettings;
            }

            _logger.LogInformation("Telegram bot settings not found in cache with key {CacheKey}. Fetching from database.", TelegramBotSettingsKey);

            TelegramBotSettingsDto settings = await GetSettingFromDbAsync<TelegramBotSettingsDto>(TelegramBotSettingsKey, () => new TelegramBotSettingsDto(), cancellationToken);

            // Corrected: SetAsync takes 3 arguments
            await _cacheService.SetAsync(TelegramBotSettingsKey, settings, _defaultCacheDuration);
            _logger.LogInformation("Telegram bot settings stored in cache with key {CacheKey}.", TelegramBotSettingsKey);

            return settings;
        }

        public async Task UpdateTelegramBotSettingsAsync(TelegramBotSettingsDto settings, CancellationToken cancellationToken = default)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            await UpdateSettingInDbAsync(TelegramBotSettingsKey, settings, cancellationToken);

            // Corrected: SetAsync takes 3 arguments
            await _cacheService.SetAsync(TelegramBotSettingsKey, settings, _defaultCacheDuration);
            _logger.LogInformation("Telegram bot settings updated in database and cache with key {CacheKey}.", TelegramBotSettingsKey);
        }

        public async Task<TelegramClientSettingsDto> GetTelegramClientSettingsAsync(CancellationToken cancellationToken = default)
        {
            // Corrected: GetAsync takes 1 argument
            TelegramClientSettingsDto? cachedSettings = await _cacheService.GetAsync<TelegramClientSettingsDto>(TelegramClientSettingsKey);
            if (cachedSettings is not null)
            {
                _logger.LogTrace("Telegram client settings retrieved from cache with key {CacheKey}.", TelegramClientSettingsKey);
                return cachedSettings;
            }

            _logger.LogInformation("Telegram client settings not found in cache with key {CacheKey}. Fetching from database.", TelegramClientSettingsKey);

            TelegramClientSettingsDto settings = await GetSettingFromDbAsync<TelegramClientSettingsDto>(TelegramClientSettingsKey, () => new TelegramClientSettingsDto(), cancellationToken);

            // Corrected: SetAsync takes 3 arguments
            await _cacheService.SetAsync(TelegramClientSettingsKey, settings, _defaultCacheDuration);
            _logger.LogInformation("Telegram client settings stored in cache with key {CacheKey}.", TelegramClientSettingsKey);

            return settings;
        }

        public async Task UpdateTelegramClientSettingsAsync(TelegramClientSettingsDto settings, CancellationToken cancellationToken = default)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            await UpdateSettingInDbAsync(TelegramClientSettingsKey, settings, cancellationToken);

            // Corrected: SetAsync takes 3 arguments
            await _cacheService.SetAsync(TelegramClientSettingsKey, settings, _defaultCacheDuration);
            _logger.LogInformation("Telegram client settings updated in database and cache with key {CacheKey}.", TelegramClientSettingsKey);
        }

        // Helper method to get settings from DB
        private async Task<T> GetSettingFromDbAsync<T>(string key, Func<T> defaultFactory, CancellationToken cancellationToken) where T : class
        {
            await using NpgsqlConnection connection = new(_configuration.GetConnectionString("DefaultConnection"));
            const string sql = @"SELECT ""Value"" FROM public.""Settings"" WHERE ""Key"" = @Key;";

            string? jsonValue = await connection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(sql, new { Key = key }, cancellationToken: cancellationToken));

            if (string.IsNullOrEmpty(jsonValue))
            {
                _logger.LogWarning("No setting found in DB for key {DbKey}. Returning default.", key);
                return defaultFactory();
            }

            try
            {
                T? setting = JsonSerializer.Deserialize<T>(jsonValue);
                if (setting == null)
                {
                    _logger.LogError("Failed to deserialize setting for key {DbKey} from JSON: {JsonValue}. Returning default.", key, jsonValue);
                    return defaultFactory();
                }
                return setting;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error for key {DbKey} with value {JsonValue}. Returning default.", key, jsonValue);
                return defaultFactory();
            }
        }

        // Helper method to update settings in DB
        private async Task UpdateSettingInDbAsync<T>(string key, T settings, CancellationToken cancellationToken) where T : class
        {
            string jsonValue = JsonSerializer.Serialize(settings);

            await using NpgsqlConnection connection = new(_configuration.GetConnectionString("DefaultConnection"));
            const string sql = @"
                INSERT INTO public.""Settings"" (""Key"", ""Value"")
                VALUES (@Key, @JsonValue::jsonb)
                ON CONFLICT (""Key"") DO UPDATE
                SET ""Value"" = @JsonValue::jsonb;";

            await connection.ExecuteAsync(
                new CommandDefinition(sql, new { Key = key, JsonValue = jsonValue }, cancellationToken: cancellationToken));
            _logger.LogInformation("Setting for key {DbKey} updated in database.", key);
        }
    }
}