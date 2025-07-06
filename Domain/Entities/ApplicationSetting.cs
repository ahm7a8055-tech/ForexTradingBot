using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities
{
    [Table("ApplicationSettings")] // Explicitly naming the table
    public class ApplicationSetting
    {
        [Key]
        [Required]
        [MaxLength(255)] // Max key length
        public string SettingKey { get; set; } = string.Empty;

        [Column(TypeName = "TEXT")] // Ensure it can hold potentially long encrypted strings
        public string? SettingValue { get; set; }

        [Required]
        public bool IsEncrypted { get; set; } = false;

        [MaxLength(1024)] // Max description length
        public string? Description { get; set; }

        [Required]
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;

        // Parameterless constructor for EF Core
        public ApplicationSetting() { }

        // Constructor for easy creation
        public ApplicationSetting(string key, string? value, bool isEncrypted, string? description = null)
        {
            SettingKey = key;
            SettingValue = value;
            IsEncrypted = isEncrypted;
            Description = description;
            LastModifiedUtc = DateTime.UtcNow;
        }
    }
}
