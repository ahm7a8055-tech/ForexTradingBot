// File: Application/DTOs/Fred/FredReleaseDto.cs
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.DTOs.Fred
{
    #region FredReleaseDto
    /// <summary>
    /// Data Transfer Object representing a single economic data release from the Federal Reserve Economic Data (FRED) API.
    /// This DTO is designed specifically for deserializing the JSON response from the FRED '/release' endpoint.
    /// </summary>
    public class FredReleaseDto
    {
        #region Properties

        #region Core Information
        /// <summary>
        /// Gets or sets the unique numerical ID of the economic release.
        /// </summary>
        /// <example>53</example>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the economic release.
        /// </summary>
        /// <example>Gross Domestic Product</example>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        #endregion

        #region Metadata & Links
        /// <summary>
        /// Gets or sets a value indicating whether the release is accompanied by a press release from the source.
        /// </summary>
        /// <example>true</example>
        [JsonPropertyName("press_release")]
        public bool IsPressRelease { get; set; }

        /// <summary>
        /// Gets or sets the optional URL to the official release page on the source's website.
        /// This can be null if no link is provided by the API.
        /// </summary>
        /// <example>https://www.bea.gov/newsreleases/national/gdp/gdpnewsrelease.htm</example>
        [JsonPropertyName("link")]
        [Url(ErrorMessage = "The provided link must be a valid URL.")]
        public string? Link { get; set; }
        #endregion

        #endregion
    }
    #endregion
}