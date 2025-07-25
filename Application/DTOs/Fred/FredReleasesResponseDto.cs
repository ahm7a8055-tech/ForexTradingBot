// File: Application/DTOs/Fred/FredReleasesResponseDto.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Application.DTOs.Fred
{
    #region FredReleasesResponseDto
    /// <summary>
    /// Represents the root object of a response from the Federal Reserve Economic Data (FRED) API
    /// for endpoints that return a list of economic data releases.
    /// </summary>
    /// <remarks>
    /// This DTO includes metadata about the request, such as pagination and sorting,
    /// as well as the main payload of release data.
    /// </remarks>
    public class FredReleasesResponseDto
    {
        #region Properties

        #region Response Metadata
        /// <summary>
        /// Gets or sets the start of the real-time period, in "YYYY-MM-DD" format.
        /// </summary>
        /// <example>2024-05-22</example>
        [JsonPropertyName("realtime_start")]
        public string RealtimeStart { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the end of the real-time period, in "YYYY-MM-DD" format.
        /// </summary>
        /// <example>2024-05-22</example>
        [JsonPropertyName("realtime_end")]
        public string RealtimeEnd { get; set; } = string.Empty;
        #endregion

        #region Pagination & Sorting
        /// <summary>
        /// Gets or sets the field by which the results are ordered.
        /// </summary>
        /// <example>release_id</example>
        [JsonPropertyName("order_by")]
        public string OrderBy { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sort order of the results.
        /// </summary>
        /// <example>asc</example>
        [JsonPropertyName("sort_order")]
        public string SortOrder { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of results available for the given request parameters.
        /// </summary>
        /// <example>286</example>
        [JsonPropertyName("count")]
        public int Count { get; set; }

        /// <summary>
        /// Gets or sets the starting offset of the returned results.
        /// </summary>
        /// <example>0</example>
        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of results returned in this response.
        /// </summary>
        /// <example>1000</example>
        [JsonPropertyName("limit")]
        public int Limit { get; set; }
        #endregion

        #region Response Payload
        /// <summary>
        /// Gets or sets the list of economic data releases returned by the API.
        /// </summary>
        [JsonPropertyName("releases")]
        public List<FredReleaseDto> Releases { get; set; } = [];
        #endregion

        #endregion
    }
    #endregion
}