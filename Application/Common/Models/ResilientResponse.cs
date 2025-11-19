// File: Application/Common/Models/ResilientResponse.cs (Updated)

using System.Text.Json.Serialization;

namespace Application.Common.Models;

/// <summary>
/// Defines the high-level type of a resilient response, guiding actions like retry or fail.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResilientResponseType
{
    Success,
    RetryableError,
    NonRetryableError
}

/// <summary>
/// Contains structured error details for a failed response, now including the Google-specific status.
/// </summary>
/// <param name="Code">The standard HTTP status code (e.g., 429, 500).</param>
/// <param name="Reason">A human-readable explanation of the error.</param>
/// <param name="Status">The specific error status from the Gemini API (e.g., "RESOURCE_EXHAUSTED", "INVALID_ARGUMENT").</param>
public record ErrorDetails(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("status")] string? Status = null
);

/// <summary>
/// A standardized, predictable response wrapper for resilient operations.
/// This model is designed to be easily consumed by monitoring systems, Hangfire, and Polly.
/// </summary>
/// <typeparam name="T">The type of the successful data payload.</typeparam>
public class ResilientResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; private init; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; private init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorDetails? Error { get; private init; }

    [JsonPropertyName("retryAfter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RetryAfter { get; private init; } // In seconds

    [JsonPropertyName("type")]
    public ResilientResponseType Type { get; private init; }

    // Private constructor to enforce usage of static factory methods.
    private ResilientResponse() { }

    #region Factory Methods

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static ResilientResponse<T> CreateSuccess(T data)
    {
        return new()
        {
            Success = true,
            Data = data,
            Type = ResilientResponseType.Success
        };
    }

    /// <summary>
    /// Creates a response for a temporary, retryable error.
    /// </summary>
    /// <param name="code">The HTTP status code.</param>
    /// <param name="reason">The human-readable error reason.</param>
    /// <param name="status">The optional Google-specific error status code.</param>
    /// <param name="retryAfterSeconds">The recommended delay before the next attempt.</param>
    public static ResilientResponse<T> CreateRetryableError(int code, string reason, string? status = null, int? retryAfterSeconds = null)
    {
        return new()
        {
            Success = false,
            Type = ResilientResponseType.RetryableError,
            Error = new ErrorDetails(code, reason, status),
            RetryAfter = retryAfterSeconds
        };
    }

    /// <summary>
    /// Creates a response for a permanent, non-retryable error.
    /// </summary>
    /// <param name="code">The HTTP status code.</param>
    /// <param name="reason">The human-readable error reason.</param>
    /// <param name="status">The optional Google-specific error status code.</param>
    public static ResilientResponse<T> CreateNonRetryableError(int code, string reason, string? status = null)
    {
        return new()
        {
            Success = false,
            Type = ResilientResponseType.NonRetryableError,
            Error = new ErrorDetails(code, reason, status)
        };
    }

    #endregion
}