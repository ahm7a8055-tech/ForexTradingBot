// File: Application/Common/Models/GeminiErrors.cs (New File)

namespace Application.Common.Models;

/// <summary>
/// A static factory for creating standardized ResilientResponse objects based on official Gemini API error codes.
/// This centralizes error handling logic and makes the consuming service cleaner.
/// </summary>
public static class GeminiErrors
{
    // --- Non-Retryable Errors ---

    /// <summary>
    /// HTTP 400: The request body is malformed (e.g., typo, missing field).
    /// This is a permanent failure for this request.
    /// </summary>
    public static ResilientResponse<T> InvalidArgument<T>(string details)
    {
        return ResilientResponse<T>.CreateNonRetryableError(400, $"Invalid Argument: The request is malformed. Details: {details}", "INVALID_ARGUMENT");
    }

    /// <summary>
    /// HTTP 400: Free tier is not available in the region, and billing is not enabled.
    /// This is a permanent failure until the user takes action on their Google project.
    /// </summary>
    public static ResilientResponse<T> FailedPrecondition<T>()
    {
        return ResilientResponse<T>.CreateNonRetryableError(400, "Failed Precondition: Gemini API free tier not available or billing not enabled.", "FAILED_PRECONDITION");
    }

    /// <summary>
    /// HTTP 403: The API key lacks required permissions or is invalid.
    /// While the job might retry with another key, this specific key has failed permanently.
    /// </summary>
    public static ResilientResponse<T> PermissionDenied<T>()
    {
        return ResilientResponse<T>.CreateNonRetryableError(403, "Permission Denied: The API key is invalid or lacks necessary permissions.", "PERMISSION_DENIED");
    }

    // Note: The key rotation logic will treat this as a reason to try the NEXT key.

    /// <summary>
    /// HTTP 404: A resource referenced in the request was not found.
    /// This is a permanent failure for this request.
    /// </summary>
    public static ResilientResponse<T> NotFound<T>(string resourceDetails)
    {
        return ResilientResponse<T>.CreateNonRetryableError(404, $"Not Found: The requested resource was not found. Details: {resourceDetails}", "NOT_FOUND");
    }


    // --- Retryable Errors ---

    /// <summary>
    /// HTTP 429: The rate limit has been exceeded.
    /// This is a temporary failure. A retry after a delay is recommended.
    /// </summary>
    public static ResilientResponse<T> ResourceExhausted<T>(int retryAfterSeconds = 60)
    {
        return ResilientResponse<T>.CreateRetryableError(429, "Resource Exhausted: Rate limit exceeded. Please wait and retry.", "RESOURCE_EXHAUSTED", retryAfterSeconds);
    }

    /// <summary>
    /// HTTP 500: An unexpected internal error occurred on Google's side.
    /// This is a temporary failure. Retrying may resolve the issue.
    /// </summary>
    public static ResilientResponse<T> InternalError<T>(int retryAfterSeconds = 30)
    {
        return ResilientResponse<T>.CreateRetryableError(500, "Internal Server Error: An unexpected error occurred on Google's side. Please retry.", "INTERNAL", retryAfterSeconds);
    }

    /// <summary>
    /// HTTP 503: The service is temporarily overloaded or unavailable.
    /// This is a temporary failure. Retrying may resolve the issue.
    /// </summary>
    public static ResilientResponse<T> ServiceUnavailable<T>(int retryAfterSeconds = 60)
    {
        return ResilientResponse<T>.CreateRetryableError(503, "Service Unavailable: The service is temporarily overloaded. Please retry.", "UNAVAILABLE", retryAfterSeconds);
    }

    /// <summary>
    /// HTTP 504: The service could not complete the request within the deadline.
    /// This can be a temporary issue or indicate the request is too complex. A retry is warranted.
    /// </summary>
    public static ResilientResponse<T> DeadlineExceeded<T>(int retryAfterSeconds = 30)
    {
        return ResilientResponse<T>.CreateRetryableError(504, "Deadline Exceeded: The request timed out. Please retry.", "DEADLINE_EXCEEDED", retryAfterSeconds);
    }
}