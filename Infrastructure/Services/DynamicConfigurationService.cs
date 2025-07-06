using Application.Common.Interfaces;
using Application.DTOs.Admin; // For DynamicSettingDto
using Domain.Entities;
using Infrastructure.Data; // For AppDbContext
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection; // For IServiceScopeFactory
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class DynamicConfigurationService : IDynamicConfigurationService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ISettingsProtectionService _protectionService;
        private readonly IConfiguration _configuration; // To check environment variables and appsettings.json
        private readonly ILogger<DynamicConfigurationService> _logger;

        // In-memory registry of defined settings (key, sensitivity, description, appsettings.json default)
        private readonly Dictionary<string, SettingDefinition> _definedSettings = new Dictionary<string, SettingDefinition>(StringComparer.OrdinalIgnoreCase);

        private record SettingDefinition(string Key, string? DefaultValueFromConfig, bool IsSensitive, string? Description);

        public DynamicConfigurationService(
            IServiceScopeFactory serviceScopeFactory,
            ISettingsProtectionService protectionService,
            IConfiguration configuration,
            ILogger<DynamicConfigurationService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _protectionService = protectionService;
            _configuration = configuration;
            _logger = logger;

            // Populate defined settings (this could also be done via DI or a config file)
            // For now, it's empty; settings need to be registered via RegisterSettingDefinition.
            // In a real app, you'd call RegisterSettingDefinition for all known configurable settings at startup.
        }

        public void RegisterSettingDefinition(string key, string? defaultValueFromConfig, bool isSensitive, string? description)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            _definedSettings[key] = new SettingDefinition(key, defaultValueFromConfig, isSensitive, description);
            _logger.LogDebug("Registered setting definition for key: {SettingKey}", key);
        }

        public IEnumerable<string> GetAllDefinedSettingKeys()
        {
            return _definedSettings.Keys.ToList();
        }

        public async Task<IDynamicSetting?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!_definedSettings.TryGetValue(key, out var definition))
            {
                _logger.LogWarning("Attempted to get undefined setting: {SettingKey}", key);
                return null; // Or throw, depending on desired behavior for undefined keys
            }

            string? envVarValue = _configuration[key.Replace(":", "__")]; // Env vars use __ for :
            bool isOverriddenByEnv = !string.IsNullOrEmpty(envVarValue);

            ApplicationSetting? dbSetting = null;
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbSetting = await dbContext.ApplicationSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SettingKey == key, cancellationToken);
            }

            string? currentValueInDb = dbSetting?.SettingValue;
            string? effectiveValue; // This is the raw value (possibly encrypted) the app would use or has from DB/Config
            string? displayValue;   // This is for the UI

            bool isPersistedInDb = dbSetting != null;
            DateTime? lastModifiedUtc = dbSetting?.LastModifiedUtc;

            if (isOverriddenByEnv)
            {
                effectiveValue = envVarValue;
                displayValue = definition.IsSensitive ? "***** (From Environment Variable)" : effectiveValue;
            }
            else if (isPersistedInDb && currentValueInDb != null)
            {
                effectiveValue = currentValueInDb; // This is still raw from DB (might be encrypted)
                if (definition.IsSensitive)
                {
                    displayValue = "***** (Set in Database)";
                }
                else
                {
                    // Non-sensitive value from DB is shown directly
                    displayValue = currentValueInDb;
                }
            }
            else // Not in Env, Not in DB - use appsettings.json default
            {
                effectiveValue = definition.DefaultValueFromConfig;
                displayValue = definition.IsSensitive && !string.IsNullOrEmpty(effectiveValue) ? "***** (From Default Config)" : effectiveValue;
            }

            return new DynamicSettingDto(
                key,
                effectiveValue, // Raw value for internal logic, might be encrypted if from DB and sensitive
                displayValue,
                definition.IsSensitive,
                definition.Description,
                isPersistedInDb,
                isOverriddenByEnv,
                lastModifiedUtc
            );
        }

        public async Task<string?> GetDecryptedValueAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!_definedSettings.TryGetValue(key, out var definition))
            {
                // For internal consumption, if a key isn't defined but is requested,
                // it might imply an issue or an ad-hoc key.
                // Fallback to direct configuration lookup for maximum flexibility,
                // assuming it's not a UI-managed dynamic setting.
                _logger.LogDebug("GetDecryptedValueAsync called for key '{SettingKey}' not explicitly defined. Checking IConfiguration.", key);
                return _configuration[key.Replace(":", "__")] ?? _configuration[key];
            }

            string? envVarValue = _configuration[key.Replace(":", "__")];
            if (!string.IsNullOrEmpty(envVarValue))
            {
                return envVarValue; // Environment variables are king and assumed plaintext
            }

            ApplicationSetting? dbSetting = null;
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbSetting = await dbContext.ApplicationSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SettingKey == key, cancellationToken);
            }

            if (dbSetting?.SettingValue != null)
            {
                if (dbSetting.IsEncrypted && definition.IsSensitive)
                {
                    try
                    {
                        return _protectionService.Decrypt(dbSetting.SettingValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to decrypt setting '{SettingKey}' from database. Returning null or default.", key);
                        // Fallback to default from config if decryption fails
                        return definition.DefaultValueFromConfig;
                    }
                }
                return dbSetting.SettingValue; // Not encrypted or not sensitive
            }

            // Not in Env, Not in DB - return appsettings.json default
            return definition.DefaultValueFromConfig;
        }


        public async Task<IEnumerable<IDynamicSetting>> GetAllSettingsAsync(CancellationToken cancellationToken = default)
        {
            var allSettings = new List<IDynamicSetting>();
            Dictionary<string, ApplicationSetting> dbSettings;

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbSettings = await dbContext.ApplicationSettings
                    .AsNoTracking()
                    .ToDictionaryAsync(s => s.SettingKey, s => s, cancellationToken);
            }

            foreach (var key in _definedSettings.Keys.OrderBy(k => k))
            {
                if (!_definedSettings.TryGetValue(key, out var definition)) continue;

                string? envVarValue = _configuration[key.Replace(":", "__")];
                bool isOverriddenByEnv = !string.IsNullOrEmpty(envVarValue);

                dbSettings.TryGetValue(key, out ApplicationSetting? dbSetting);

                string? currentValueInDb = dbSetting?.SettingValue;
                string? effectiveValue;
                string? displayValue;
                bool isPersistedInDb = dbSetting != null;
                DateTime? lastModifiedUtc = dbSetting?.LastModifiedUtc;

                if (isOverriddenByEnv)
                {
                    effectiveValue = envVarValue;
                    displayValue = definition.IsSensitive ? "***** (From Environment Variable)" : effectiveValue;
                }
                else if (isPersistedInDb && currentValueInDb != null)
                {
                    effectiveValue = currentValueInDb; // Raw from DB
                    displayValue = definition.IsSensitive ? "***** (Set in Database)" : currentValueInDb;
                }
                else
                {
                    effectiveValue = definition.DefaultValueFromConfig;
                    displayValue = definition.IsSensitive && !string.IsNullOrEmpty(effectiveValue) ? "***** (From Default Config)" : effectiveValue;
                }

                allSettings.Add(new DynamicSettingDto(
                    key,
                    effectiveValue, // Raw value for internal logic
                    displayValue,
                    definition.IsSensitive,
                    definition.Description,
                    isPersistedInDb,
                    isOverriddenByEnv,
                    lastModifiedUtc
                ));
            }
            return allSettings;
        }

        public async Task UpdateSettingsAsync(Dictionary<string, string?> settingsToUpdate, CancellationToken cancellationToken = default)
        {
            if (settingsToUpdate == null) throw new ArgumentNullException(nameof(settingsToUpdate));

            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var kvp in settingsToUpdate)
                {
                    var key = kvp.Key;
                    var newValue = kvp.Value; // This is the raw, plaintext new value from UI

                    if (!_definedSettings.TryGetValue(key, out var definition))
                    {
                        _logger.LogWarning("Attempted to update undefined setting: {SettingKey}. Skipping.", key);
                        continue; // Or throw, based on strictness
                    }

                    ApplicationSetting? dbSetting = await dbContext.ApplicationSettings
                        .FirstOrDefaultAsync(s => s.SettingKey == key, cancellationToken);

                    if (dbSetting == null)
                    {
                        dbSetting = new ApplicationSetting
                        {
                            SettingKey = key,
                            Description = definition.Description,
                            IsEncrypted = definition.IsSensitive
                        };
                        dbContext.ApplicationSettings.Add(dbSetting);
                    }

                    dbSetting.IsEncrypted = definition.IsSensitive; // Ensure this flag is correctly set based on definition
                    if (definition.IsSensitive)
                    {
                        dbSetting.SettingValue = string.IsNullOrEmpty(newValue) ? null : _protectionService.Encrypt(newValue);
                    }
                    else
                    {
                        dbSetting.SettingValue = newValue;
                    }
                    dbSetting.LastModifiedUtc = DateTime.UtcNow;
                    _logger.LogInformation("Setting '{SettingKey}' updated in database (value {IsSensitive}).", key, definition.IsSensitive ? "is sensitive and was encrypted" : "is not sensitive");
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("{Count} settings updated successfully in the database.", settingsToUpdate.Count);

                // Trigger a configuration reload
                Configuration.DatabaseConfigurationProvider.TriggerReload();
                _logger.LogInformation("DatabaseConfigurationProvider reload triggered due to settings update.");
                _logger.LogWarning("Dynamic settings updated. IOptionsMonitor should pick up changes. Some services might still require a restart for certain fundamental settings to take full effect (e.g., connection strings used at initial DI setup).");

            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error updating settings in database. Transaction rolled back.");
                throw; // Re-throw to indicate failure
            }
        }
    }
}
