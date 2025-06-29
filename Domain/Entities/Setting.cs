// --- REVISED FILE: Domain/Entities/Setting.cs ---
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    /// <summary>
    /// Represents a key-value pair for application-wide settings stored in the database.
    /// The Key itself is the primary key.
    /// </summary>
    public class Setting // NO INHERITANCE
    {
        [Key]
        [MaxLength(256)]
        public string Key { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }
}