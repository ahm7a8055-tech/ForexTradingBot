using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Application.Common.Interfaces
{
    public interface IDynamicSetting
    {
        string Key { get; }
        string? Value { get; } // Raw value, could be encrypted
        string? DisplayValue { get; } // Value safe for display (e.g., masked)
        bool IsSensitive { get; }
        string? Description { get; }
        bool IsPersistedInDb { get; } // Indicates if the current value comes from the DB
        bool IsOverriddenByEnvironment { get; } // Indicates if an env var is overriding this
    }

    public interface IDynamicConfigurationService
    {
        /// <summary>
        /// Gets a specific setting by its key.
        /// The implementation will handle decryption for sensitive values internally if needed for application use,
        /// but will return a masked value for display if the setting is sensitive.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The setting DTO, or null if not found.</returns>
        Task<IDynamicSetting?> GetSettingAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the string value of a setting, appropriately decrypted if sensitive.
        /// Returns null if the setting is not found or has no value.
        /// This is intended for application services to consume configuration values.
        /// </summary>
        Task<string?> GetDecryptedValueAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all manageable dynamic settings.
        /// Sensitive values will be masked for display.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of all dynamic settings.</returns>
        Task<IEnumerable<IDynamicSetting>> GetAllSettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates one or more settings in the database.
        /// Handles encryption for sensitive values.
        /// </summary>
        /// <param name="settingsToUpdate">A dictionary where Key is the setting key and Value is the new raw string value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpdateSettingsAsync(Dictionary<string, string?> settingsToUpdate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers a setting definition, including its default value from appsettings.json if not in DB,
        /// sensitivity, and description. This helps the service know about all possible settings.
        /// </summary>
        void RegisterSettingDefinition(string key, string? defaultValueFromConfig, bool isSensitive, string? description);

        /// <summary>
        /// Gets all defined setting keys.
        /// </summary>
        IEnumerable<string> GetAllDefinedSettingKeys();
    }
}
