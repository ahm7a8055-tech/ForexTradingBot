using Application.Common.Interfaces;
using Application.DTOs.Admin; // For DynamicSettingDto
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data; // For AppDbContext
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection; // For IServiceScopeFactory
using Microsoft.Extensions.Logging;
using Shared.Security; // For SecureExceptionSanitizer
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Infrastructure.Services
{
    /// <summary>
    /// Service for managing dynamic configuration settings with encryption support.
    /// Provides secure storage and retrieval of application settings.
    /// </summary>
    public class DynamicConfigurationService : IDynamicConfigurationService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ISettingsProtectionService _protectionService;
        private readonly IConfiguration _configuration; // To check environment variables and appsettings.json
        private readonly ILogger<DynamicConfigurationService> _logger;

        // Store defined settings with their metadata
        private readonly Dictionary<string, SettingDefinition> _definedSettings = new Dictionary<string, SettingDefinition>(StringComparer.OrdinalIgnoreCase);

        private record SettingDefinition(string Key, string? DefaultValueFromConfig, bool IsSensitive, string? Description);

        public DynamicConfigurationService(
            IServiceScopeFactory serviceScopeFactory,
            ISettingsProtectionService protectionService,
            IConfiguration configuration,
            ILogger<DynamicConfigurationService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _protectionService = protectionService ?? throw new ArgumentNullException(nameof(protectionService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Security Methods
        /// <summary>
        /// Sanitizes input for safe logging by removing newlines and other problematic characters.
        /// </summary>
        /// <param name="input">The input to sanitize</param>
        /// <returns>Sanitized string safe for logging</returns>
        private static string SanitizeForLogging(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "[EMPTY_INPUT]";

            // Remove newlines, carriage returns, and other problematic characters
            var sanitized = input
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", " ")
                .Replace("\0", ""); // Null characters

            // Remove any remaining control characters
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[\x00-\x1F\x7F]", "");

            // Limit length to prevent log flooding
            if (sanitized.Length > 200)
            {
                sanitized = sanitized[..200] + "...";
            }

            return sanitized;
        }

        /// <summary>
        /// Sanitizes sensitive data by redacting sensitive patterns while preserving structure.
        /// </summary>
        /// <param name="sensitiveInput">The sensitive input to sanitize</param>
        /// <returns>Sanitized string with sensitive data redacted</returns>
        private static string SanitizeSensitiveData(string? sensitiveInput)
        {
            if (string.IsNullOrWhiteSpace(sensitiveInput))
                return "[EMPTY_INPUT]";

            var sanitized = sensitiveInput;

            // Redact connection string patterns
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"(?:password|pwd)\s*=\s*[^;\s]+", "password=***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"(?:user\s*id|uid|username)\s*=\s*[^;\s]+", "userid=***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Redact bot token patterns
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"[0-9]+:[a-zA-Z0-9\-_]{35}", "***:***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Redact API keys and tokens
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"[a-zA-Z0-9\-_]{20,}", "***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return sanitized;
        }

        /// <summary>
        /// Validates setting key format to prevent injection attacks.
        /// </summary>
        /// <param name="key">The setting key to validate</param>
        /// <returns>Validated key or null if invalid</returns>
        private string? ValidateSettingKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            // SECURITY: Sanitize input before validation
            var sanitizedKey = SanitizeForLogging(key);
            
            // Basic validation - check for required format
            if (!key.Contains(":") || key.Length > 100)
            {
                _logger.LogWarning("Invalid setting key format: {SanitizedKey}. Key must contain ':' and be less than 100 characters.", sanitizedKey);
                return null;
            }

            // Check for potentially dangerous characters
            if (key.Contains("..") || key.Contains("\\") || key.Contains("/"))
            {
                _logger.LogWarning("Setting key contains potentially dangerous characters: {SanitizedKey}", sanitizedKey);
                return null;
            }

            return key;
        }
        #endregion

        public void RegisterSettingDefinition(string key, string? defaultValueFromConfig, bool isSensitive, string? description)
        {
            // SECURITY: Validate setting key before registration
            var validatedKey = ValidateSettingKey(key);
            if (validatedKey == null)
            {
                _logger.LogWarning("Cannot register setting definition: Invalid key format.");
                return;
            }

            // SECURITY: Sanitize description before logging
            var sanitizedDescription = SanitizeForLogging(description);
            var sanitizedDefaultValue = isSensitive ? "[SENSITIVE_DEFAULT]" : SanitizeForLogging(defaultValueFromConfig);
            
            _logger.LogDebug("Registering setting definition: {SanitizedKey}, IsSensitive: {IsSensitive}, Description: {SanitizedDescription}, Default: {SanitizedDefaultValue}", 
                SanitizeForLogging(validatedKey), isSensitive, sanitizedDescription, sanitizedDefaultValue);

            _definedSettings[validatedKey] = new SettingDefinition(validatedKey, defaultValueFromConfig, isSensitive, description);
        }

        public IEnumerable<string> GetAllDefinedSettingKeys()
        {
            return _definedSettings.Keys.OrderBy(k => k).ToList();
        }

        public async Task<IDynamicSetting?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
        {
            var validatedKey = ValidateSettingKey(key);
            if (validatedKey == null)
            {
                _logger.LogWarning("GetSettingAsync called with invalid key format.");
                return null;
            }

            if (!_definedSettings.TryGetValue(validatedKey, out var definition))
            {
                // SECURITY: Sanitize key before logging
                var sanitizedKey = SanitizeForLogging(validatedKey);
                _logger.LogDebug("GetSettingAsync called for key '{SanitizedKey}' not explicitly defined. Checking IConfiguration.", sanitizedKey);
                return null;
            }

            string? envVarValue = _configuration[validatedKey.Replace(":", "__")];
            bool isOverriddenByEnv = !string.IsNullOrEmpty(envVarValue);

            ApplicationSetting? dbSetting = null;
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbSetting = await dbContext.ApplicationSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SettingKey == validatedKey, cancellationToken);
            }

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

            return new DynamicSettingDto(
                validatedKey,
                effectiveValue, // Raw value for internal logic
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
            var validatedKey = ValidateSettingKey(key);
            if (validatedKey == null)
            {
                _logger.LogWarning("GetDecryptedValueAsync called with invalid key format.");
                return null;
            }

            if (!_definedSettings.TryGetValue(validatedKey, out var definition))
            {
                // For internal consumption, if a key isn't defined but is requested,
                // it might imply an issue or an ad-hoc key.
                // Fallback to direct configuration lookup for maximum flexibility,
                // assuming it's not a UI-managed dynamic setting.
                _logger.LogDebug("GetDecryptedValueAsync called for key '{SanitizedKey}' not explicitly defined. Checking IConfiguration.", 
                    SanitizeForLogging(validatedKey));
                return _configuration[validatedKey.Replace(":", "__")] ?? _configuration[validatedKey];
            }

            string? envVarValue = _configuration[validatedKey.Replace(":", "__")];
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
                    .FirstOrDefaultAsync(s => s.SettingKey == validatedKey, cancellationToken);
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
                        // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                        var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                        _logger.LogError(sanitizedException, "Failed to decrypt setting '{SanitizedKey}' from database. Returning null or default.", 
                            SanitizeForLogging(validatedKey));
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

                    // SECURITY: Validate setting key before processing
                    var validatedKey = ValidateSettingKey(key);
                    if (validatedKey == null)
                    {
                        _logger.LogWarning("Attempted to update setting with invalid key format. Skipping.");
                        continue;
                    }

                    if (!_definedSettings.TryGetValue(validatedKey, out var definition))
                    {
                        _logger.LogWarning("Attempted to update undefined setting: {SanitizedKey}. Skipping.", 
                            SanitizeForLogging(validatedKey));
                        continue; // Or throw, based on strictness
                    }

                    ApplicationSetting? dbSetting = await dbContext.ApplicationSettings
                        .FirstOrDefaultAsync(s => s.SettingKey == validatedKey, cancellationToken);

                    if (dbSetting == null)
                    {
                        dbSetting = new ApplicationSetting
                        {
                            SettingKey = validatedKey,
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
                    
                    // SECURITY: Use sanitized key for logging
                    _logger.LogInformation("Setting '{SanitizedKey}' updated in database (value {IsSensitive}).", 
                        SanitizeForLogging(validatedKey), 
                        definition.IsSensitive ? "is sensitive and was encrypted" : "is not sensitive");
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
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Error updating settings in database. Transaction rolled back.");
                throw; // Re-throw to indicate failure
            }
        }
    }
}
