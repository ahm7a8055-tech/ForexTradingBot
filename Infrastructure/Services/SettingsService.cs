// --- START OF CORRECTED FILE: Infrastructure/Services/SettingsService.cs ---

using Application.Common.Interfaces;
using Application.DTOs.Settings;
using Application.Interfaces;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    /// <summary>
    /// Implements the service for retrieving application settings, using a cache-aside pattern for performance.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly ICacheService _cacheService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SettingsService> _logger;
        private const string ForceJoinSettingsKey = "settings:force_join"; // The key for both DB and Cache

        public SettingsService(ICacheService cacheService, IConfiguration configuration, ILogger<SettingsService> logger)
        {
            _cacheService = cacheService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ForceJoinSettingsDto> GetForceJoinSettingsAsync(CancellationToken cancellationToken = default)
        {
            // 1. Try to get from cache first
            // --- FIX: Removed cancellationToken from this call ---
            var cachedSettings = await _cacheService.GetAsync<ForceJoinSettingsDto>(ForceJoinSettingsKey);
            if (cachedSettings is not null)
            {
                _logger.LogTrace("Force join settings retrieved from cache.");
                return cachedSettings;
            }

            // 2. If not in cache, get from the database
            _logger.LogInformation("Force join settings not found in cache. Fetching from database.");
            await using var connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            const string sql = @"SELECT ""Value"" FROM public.""Settings"" WHERE ""Key"" = @Key;";

            var jsonValue = await connection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(sql, new { Key = ForceJoinSettingsKey }, cancellationToken: cancellationToken));

            ForceJoinSettingsDto settings;
            if (string.IsNullOrEmpty(jsonValue))
            {
                // If no setting exists in DB, create a default "disabled" one.
                settings = new ForceJoinSettingsDto { IsEnabled = false };
            }
            else
            {
                settings = JsonSerializer.Deserialize<ForceJoinSettingsDto>(jsonValue) ?? new ForceJoinSettingsDto();
            }

            // 3. Store the result in the cache for future requests.
            // Cache for 5 minutes. The cache is invalidated immediately by AdminService when settings are changed.
            // --- FIX: Removed cancellationToken from this call. The method now takes 3 arguments. ---
            await _cacheService.SetAsync(ForceJoinSettingsKey, settings, TimeSpan.FromMinutes(5));

            return settings;
        }
    }
}