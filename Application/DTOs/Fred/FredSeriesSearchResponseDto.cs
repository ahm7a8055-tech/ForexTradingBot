// File: Application/DTOs/Fred/FredSeriesSearchResponseDto.cs
using Application.Common.Interfaces.Fred;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Application.DTOs.Fred
{
    #region FredSeriesSearchResponseDto
    /// <summary>
    /// Represents the root object of a response from the Federal Reserve Economic Data (FRED) API
    /// for a series search request (e.g., from the '/fred/series/search' endpoint).
    /// </summary>
    /// <remarks>
    /// This DTO includes pagination metadata and the main payload of matching series data.
    /// </remarks>
    public class FredSeriesSearchResponseDto
    {
        #region Properties

        #region Pagination
        /// <summary>
        /// Gets or sets the total number of series results available for the given search query.
        /// </summary>
        /// <example>45</example>
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
        /// Gets or sets the list of economic data series matching the search criteria.
        /// </summary>
        /// <remarks>
        /// The <see cref="JsonPropertyNameAttribute"/> is set to "seriess" to correctly handle the
        /// known naming quirk in the FRED API's JSON response for this endpoint.
        /// </remarks>
        [JsonPropertyName("seriess")]
        public List<FredSeriesDto> Series { get; set; } = new();
        #endregion

        #endregion
    }
    #endregion
}