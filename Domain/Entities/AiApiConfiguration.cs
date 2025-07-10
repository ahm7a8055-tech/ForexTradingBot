using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities
{
    /// <summary>
    /// Represents the configuration for an external AI API service, stored in the database.
    /// This entity is designed to be flexible enough to hold settings for various providers
    /// like Gemini, OpenAI, Anthropic, etc.
    /// </summary>
    [Table("AiApiConfigurations")] // Recommended to use pluralized table name
    public class AiApiConfiguration
    {
        /// <summary>
        /// Primary key for the configuration record.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The unique, machine-readable name of the provider (e.g., "Gemini", "OpenAI").
        /// This is the primary key for looking up a service's configuration.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// A flag to easily enable or disable this entire AI integration from the database
        /// without deleting the configuration.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// The API key for the service. It is highly recommended to encrypt this value
        /// at the application layer before storing it in the database.
        /// </summary>
        [Required]
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// The specific model to use for the API calls (e.g., "gemini-1.5-flash-latest", "gpt-4o").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// The prompt template to be used for generating content. Must contain a placeholder
        /// (e.g., "{message}") that will be replaced with the user's input.
        /// </summary>
        [Required]
        public string PromptTemplate { get; set; } = string.Empty;

        /// <summary>
        /// A human-readable description for administrators to understand the purpose of this configuration.
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// The UTC date and time when this record was created.
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// The UTC date and time when this record was last updated.
        /// </summary>
        [Required]
        public DateTime LastUpdatedAt { get; set; }

        /// <summary>
        /// The name of the API key used for this configuration.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ApiKeyName { get; set; } = "Default";

        /// <summary>
        /// Default constructor. Initializes with sensible defaults.
        /// </summary>
        public AiApiConfiguration()
        {
            IsEnabled = true;
            CreatedAt = DateTime.UtcNow;
            LastUpdatedAt = DateTime.UtcNow;
        }
    }
}