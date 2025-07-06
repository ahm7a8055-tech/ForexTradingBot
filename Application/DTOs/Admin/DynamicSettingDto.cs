using Application.Common.Interfaces;

namespace Application.DTOs.Admin
{
    public class DynamicSettingDto : IDynamicSetting
    {
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; } // Raw value from DB, could be encrypted
        public string? DisplayValue { get; set; } // Masked if sensitive, or decrypted non-sensitive value
        public bool IsSensitive { get; set; }
        public string? Description { get; set; }
        public bool IsPersistedInDb { get; set; }
        public bool IsOverriddenByEnvironment { get; set; }
        public System.DateTime? LastModifiedUtc { get; set; } // When was it last changed in DB

        public DynamicSettingDto() { }

        public DynamicSettingDto(
            string key,
            string? value,
            string? displayValue,
            bool isSensitive,
            string? description,
            bool isPersistedInDb,
            bool isOverriddenByEnvironment,
            System.DateTime? lastModifiedUtc)
        {
            Key = key;
            Value = value; // This is the raw value from DB
            DisplayValue = displayValue;
            IsSensitive = isSensitive;
            Description = description;
            IsPersistedInDb = isPersistedInDb;
            IsOverriddenByEnvironment = isOverriddenByEnvironment;
            LastModifiedUtc = lastModifiedUtc;
        }
    }
}
