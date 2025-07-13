# GeminiService Usage Guide

## Overview

The `GeminiService` provides AI-powered message enhancement capabilities using Google's Gemini API. This guide explains how to properly integrate and use the service throughout your application.

## Table of Contents

1. [Service Architecture](#service-architecture)
2. [Integration Points](#integration-points)
3. [Usage Patterns](#usage-patterns)
4. [Best Practices](#best-practices)
5. [Configuration](#configuration)
6. [Error Handling](#error-handling)
7. [Performance Considerations](#performance-considerations)
8. [Examples](#examples)

## Service Architecture

### Core Components

- **`IGeminiService`**: Interface defining the contract for AI enhancement
- **`GeminiService`**: Implementation using Google Gemini API
- **`GeminiController`**: API endpoints for job management
- **Hangfire Integration**: Background job processing for non-blocking operations

### Key Features

- ✅ **Background Processing**: Non-blocking AI enhancement via Hangfire
- ✅ **Job Tracking**: Real-time job status monitoring
- ✅ **Batch Processing**: Efficient handling of multiple requests
- ✅ **Resilient**: Automatic retry and error handling
- ✅ **Categorized Logging**: Professional admin logging with performance metrics

## Integration Points

### 1. TelegramUserApiClient Integration

The `TelegramUserApiClient` is the primary integration point for AI enhancement:

```csharp
// Constructor injection
public TelegramUserApiClient(
    // ... other dependencies
    IGeminiService geminiService,
    // ... other dependencies
)
```

#### Usage in SendMessageAsync

```csharp
// AI Enhancement for text messages
if (!string.IsNullOrWhiteSpace(message))
{
    try
    {
        // Use Hangfire background job for AI enhancement
        var jobId = await _geminiService.EnhanceMessageAsync(message, cancellationToken);
        
        if (!string.IsNullOrWhiteSpace(jobId) && !jobId.StartsWith("Job enqueued"))
        {
            // Direct result available
            _logger.LogInformation("Message successfully enhanced by AI for Peer {PeerId}.", peerIdForLog);
            var (parsedText, parsedEntities) = _markdownParserService.ParseMarkdownToTelegramEntities(jobId);
            message = parsedText;
            entitiesArray = parsedEntities.Length > 0 ? parsedEntities : null;
        }
        else if (!string.IsNullOrWhiteSpace(jobId))
        {
            // Background job enqueued
            var actualJobId = jobId.Replace("Job enqueued successfully. JobId: ", "");
            _logger.LogInformation("AI enhancement job enqueued for Peer {PeerId}. JobId: {JobId}", peerIdForLog, actualJobId);
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "AI message enhancement failed for Peer {PeerId}. Proceeding with original text.", peerIdForLog);
    }
}
```

#### Usage in SendMediaGroupAsync

```csharp
// AI Enhancement for media captions
string? enhancedCaption = await _geminiService.EnhanceMessageAsync(albumCaption, cancellationToken);
var (currentCaption, currentEntities) = (albumCaption, albumEntities);

if (!string.IsNullOrWhiteSpace(enhancedCaption) && !enhancedCaption.StartsWith("Job enqueued"))
{
    // Direct result available
    debugReport.AppendLine($"   - ✅ AI service returned enhanced caption.");
    (currentCaption, currentEntities) = FormatFinalMessage(enhancedCaption, null, debugReport);
}
else if (!string.IsNullOrWhiteSpace(enhancedCaption))
{
    // Background job enqueued
    var actualJobId = enhancedCaption.Replace("Job enqueued successfully. JobId: ", "");
    debugReport.AppendLine($"   - 🔄 AI enhancement job enqueued. JobId: {actualJobId}");
    (currentCaption, currentEntities) = FormatFinalMessage(albumCaption, albumEntities, debugReport);
}
```

### 2. Direct Service Usage

For custom implementations, inject `IGeminiService` directly:

```csharp
public class CustomMessageProcessor
{
    private readonly IGeminiService _geminiService;
    private readonly ILogger<CustomMessageProcessor> _logger;

    public CustomMessageProcessor(IGeminiService geminiService, ILogger<CustomMessageProcessor> logger)
    {
        _geminiService = geminiService;
        _logger = logger;
    }

    public async Task<string> ProcessMessageAsync(string originalMessage, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _geminiService.EnhanceMessageAsync(originalMessage, cancellationToken);
            
            if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Job enqueued"))
            {
                return result; // Direct enhancement result
            }
            
            // Handle background job if needed
            return originalMessage; // Fallback to original
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enhance message with AI");
            return originalMessage; // Fallback to original
        }
    }
}
```

## Usage Patterns

### 1. Synchronous Enhancement (Immediate Results)

Use when you need the enhanced result immediately:

```csharp
// For immediate processing
var enhancedMessage = await _geminiService.EnhanceMessageAsync(message, cancellationToken);

if (!string.IsNullOrWhiteSpace(enhancedMessage) && !enhancedMessage.StartsWith("Job enqueued"))
{
    // Use enhanced message immediately
    return enhancedMessage;
}
```

### 2. Asynchronous Enhancement (Background Jobs)

Use when you want non-blocking processing:

```csharp
// For background processing
var jobId = await _geminiService.EnhanceMessageAsync(message, cancellationToken);

if (!string.IsNullOrWhiteSpace(jobId) && jobId.StartsWith("Job enqueued"))
{
    var actualJobId = jobId.Replace("Job enqueued successfully. JobId: ", "");
    
    // Option 1: Check job status later
    var result = await _geminiService.GetJobResultAsync(actualJobId, cancellationToken);
    
    // Option 2: Use Hangfire job monitoring
    // The job will be processed in the background
}
```

### 3. Batch Processing

For multiple messages, use batch processing:

```csharp
// Enqueue multiple enhancement jobs
var jobIds = new List<string>();
foreach (var message in messages)
{
    var jobId = await _geminiService.EnhanceMessageAsync(message, cancellationToken);
    if (!string.IsNullOrWhiteSpace(jobId) && jobId.StartsWith("Job enqueued"))
    {
        jobIds.Add(jobId.Replace("Job enqueued successfully. JobId: ", ""));
    }
}

// Monitor batch progress
foreach (var jobId in jobIds)
{
    var result = await _geminiService.GetJobResultAsync(jobId, cancellationToken);
    // Process result...
}
```

## Best Practices

### 1. Always Provide Fallbacks

```csharp
public async Task<string> EnhanceMessageWithFallbackAsync(string originalMessage, CancellationToken cancellationToken)
{
    try
    {
        var enhancedMessage = await _geminiService.EnhanceMessageAsync(originalMessage, cancellationToken);
        
        if (!string.IsNullOrWhiteSpace(enhancedMessage) && !enhancedMessage.StartsWith("Job enqueued"))
        {
            return enhancedMessage;
        }
        
        // Fallback to original message
        return originalMessage;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "AI enhancement failed, using original message");
        return originalMessage;
    }
}
```

### 2. Use Appropriate Cancellation Tokens

```csharp
// For user-initiated operations
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var enhancedMessage = await _geminiService.EnhanceMessageAsync(message, cts.Token);

// For background operations
var enhancedMessage = await _geminiService.EnhanceMessageAsync(message, CancellationToken.None);
```

### 3. Implement Proper Logging

```csharp
public async Task<string> ProcessWithLoggingAsync(string message, long peerId, CancellationToken cancellationToken)
{
    _logger.LogDebug("Starting AI enhancement for Peer {PeerId}", peerId);
    
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var result = await _geminiService.EnhanceMessageAsync(message, cancellationToken);
    stopwatch.Stop();
    
    if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Job enqueued"))
    {
        _logger.LogInformation("AI enhancement completed for Peer {PeerId} in {ElapsedMs}ms", 
            peerId, stopwatch.ElapsedMilliseconds);
        return result;
    }
    
    _logger.LogWarning("AI enhancement failed or was queued for Peer {PeerId}", peerId);
    return message;
}
```

### 4. Handle Job Status Monitoring

```csharp
public async Task<string?> WaitForJobCompletionAsync(string jobId, TimeSpan timeout, CancellationToken cancellationToken)
{
    var startTime = DateTime.UtcNow;
    
    while (DateTime.UtcNow - startTime < timeout)
    {
        var result = await _geminiService.GetJobResultAsync(jobId, cancellationToken);
        
        if (result == "JOB_RUNNING")
        {
            await Task.Delay(1000, cancellationToken);
            continue;
        }
        
        if (result == "JOB_NOT_FOUND")
            return null;
        
        return result; // Job completed
    }
    
    return null; // Timeout
}
```

## Configuration

### 1. Service Registration

```csharp
// In Program.cs or Startup.cs
services.AddSingleton<IGeminiService, GeminiService>();
services.AddScoped<MarkdownParserService>();

// Configure Hangfire for background jobs
services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString));
```

### 2. AppSettings Configuration

```json
{
  "GeminiService": {
    "ApiKey": "your-gemini-api-key",
    "Model": "gemini-pro",
    "MaxTokens": 1000,
    "Temperature": 0.7,
    "EnableBackgroundJobs": true,
    "JobTimeoutMinutes": 5
  }
}
```

### 3. Environment Variables

```bash
# Production environment
GEMINI_API_KEY=your-production-api-key
GEMINI_MODEL=gemini-pro
GEMINI_MAX_TOKENS=1000
GEMINI_TEMPERATURE=0.7
```

## Error Handling

### 1. Service-Level Error Handling

```csharp
public async Task<string> SafeEnhancementAsync(string message, CancellationToken cancellationToken)
{
    try
    {
        return await _geminiService.EnhanceMessageAsync(message, cancellationToken);
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "Network error during AI enhancement");
        return message; // Fallback
    }
    catch (TaskCanceledException ex)
    {
        _logger.LogWarning(ex, "AI enhancement timed out");
        return message; // Fallback
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error during AI enhancement");
        return message; // Fallback
    }
}
```

### 2. Job-Level Error Handling

```csharp
public async Task<string?> GetJobResultWithRetryAsync(string jobId, int maxRetries = 3, CancellationToken cancellationToken = default)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            var result = await _geminiService.GetJobResultAsync(jobId, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get job result (attempt {Attempt}/{MaxRetries})", i + 1, maxRetries);
            
            if (i == maxRetries - 1)
                throw;
                
            await Task.Delay(1000 * (i + 1), cancellationToken); // Exponential backoff
        }
    }
    
    return null;
}
```

## Performance Considerations

### 1. Caching Enhanced Results

```csharp
public class CachedGeminiService : IGeminiService
{
    private readonly IGeminiService _innerService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedGeminiService> _logger;

    public async Task<string> EnhanceMessageAsync(string message, CancellationToken cancellationToken)
    {
        var cacheKey = $"gemini_enhance_{ComputeHash(message)}";
        
        if (_cache.TryGetValue(cacheKey, out string? cachedResult))
        {
            _logger.LogDebug("Returning cached AI enhancement result");
            return cachedResult;
        }
        
        var result = await _innerService.EnhanceMessageAsync(message, cancellationToken);
        
        if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Job enqueued"))
        {
            _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
        }
        
        return result;
    }
    
    private static string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes);
    }
}
```

### 2. Rate Limiting

```csharp
public class RateLimitedGeminiService : IGeminiService
{
    private readonly IGeminiService _innerService;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<RateLimitedGeminiService> _logger;

    public RateLimitedGeminiService(IGeminiService innerService, ILogger<RateLimitedGeminiService> logger)
    {
        _innerService = innerService;
        _logger = logger;
        _semaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent requests
    }

    public async Task<string> EnhanceMessageAsync(string message, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await _innerService.EnhanceMessageAsync(message, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

## Examples

### 1. Complete Message Processing Pipeline

```csharp
public class MessageProcessor
{
    private readonly IGeminiService _geminiService;
    private readonly MarkdownParserService _markdownParser;
    private readonly ILogger<MessageProcessor> _logger;

    public async Task<ProcessedMessage> ProcessMessageAsync(string originalMessage, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Step 1: AI Enhancement
            var enhancedMessage = await _geminiService.EnhanceMessageAsync(originalMessage, cancellationToken);
            
            if (string.IsNullOrWhiteSpace(enhancedMessage) || enhancedMessage.StartsWith("Job enqueued"))
            {
                _logger.LogWarning("AI enhancement failed or was queued, using original message");
                enhancedMessage = originalMessage;
            }
            
            // Step 2: Markdown Parsing
            var (parsedText, entities) = _markdownParser.ParseMarkdownToTelegramEntities(enhancedMessage);
            
            stopwatch.Stop();
            
            return new ProcessedMessage
            {
                OriginalText = originalMessage,
                EnhancedText = enhancedMessage,
                FinalText = parsedText,
                Entities = entities,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                WasEnhanced = enhancedMessage != originalMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message processing failed");
            return new ProcessedMessage
            {
                OriginalText = originalMessage,
                EnhancedText = originalMessage,
                FinalText = originalMessage,
                Entities = Array.Empty<MessageEntity>(),
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                WasEnhanced = false,
                Error = ex.Message
            };
        }
    }
}

public class ProcessedMessage
{
    public string OriginalText { get; set; } = string.Empty;
    public string EnhancedText { get; set; } = string.Empty;
    public string FinalText { get; set; } = string.Empty;
    public MessageEntity[] Entities { get; set; } = Array.Empty<MessageEntity>();
    public long ProcessingTimeMs { get; set; }
    public bool WasEnhanced { get; set; }
    public string? Error { get; set; }
}
```

### 2. Background Job Monitoring

```csharp
public class BackgroundJobMonitor
{
    private readonly IGeminiService _geminiService;
    private readonly ILogger<BackgroundJobMonitor> _logger;

    public async Task MonitorJobAsync(string jobId, Action<string> onComplete, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMinutes(5);
        
        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                var result = await _geminiService.GetJobResultAsync(jobId, cancellationToken);
                
                if (result == "JOB_RUNNING")
                {
                    await Task.Delay(2000, cancellationToken); // Check every 2 seconds
                    continue;
                }
                
                if (result == "JOB_NOT_FOUND")
                {
                    _logger.LogWarning("Job {JobId} not found", jobId);
                    return;
                }
                
                // Job completed
                _logger.LogInformation("Job {JobId} completed successfully", jobId);
                onComplete(result);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring job {JobId}", jobId);
                await Task.Delay(5000, cancellationToken); // Wait longer on error
            }
        }
        
        _logger.LogWarning("Job {JobId} timed out", jobId);
    }
}
```

### 3. API Controller Example

```csharp
[ApiController]
[Route("api/[controller]")]
public class MessageEnhancementController : ControllerBase
{
    private readonly IGeminiService _geminiService;
    private readonly ILogger<MessageEnhancementController> _logger;

    [HttpPost("enhance")]
    public async Task<IActionResult> EnhanceMessage([FromBody] EnhancementRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _geminiService.EnhanceMessageAsync(request.Message, cancellationToken);
            
            if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Job enqueued"))
            {
                return Ok(new EnhancementResponse
                {
                    EnhancedMessage = result,
                    JobId = null,
                    Status = "Completed"
                });
            }
            
            if (!string.IsNullOrWhiteSpace(result) && result.StartsWith("Job enqueued"))
            {
                var jobId = result.Replace("Job enqueued successfully. JobId: ", "");
                return Accepted(new EnhancementResponse
                {
                    EnhancedMessage = null,
                    JobId = jobId,
                    Status = "Queued"
                });
            }
            
            return BadRequest("Enhancement failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enhancement request failed");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("job/{jobId}")]
    public async Task<IActionResult> GetJobStatus(string jobId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _geminiService.GetJobResultAsync(jobId, cancellationToken);
            
            return Ok(new JobStatusResponse
            {
                JobId = jobId,
                Status = result,
                CompletedAt = result != "JOB_RUNNING" && result != "JOB_NOT_FOUND" ? DateTime.UtcNow : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job status for {JobId}", jobId);
            return StatusCode(500, "Internal server error");
        }
    }
}

public class EnhancementRequest
{
    public string Message { get; set; } = string.Empty;
}

public class EnhancementResponse
{
    public string? EnhancedMessage { get; set; }
    public string? JobId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class JobStatusResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
}
```

## Conclusion

The GeminiService provides a robust, scalable solution for AI-powered message enhancement. By following these patterns and best practices, you can effectively integrate AI capabilities into your application while maintaining performance, reliability, and user experience.

Remember to:
- Always provide fallbacks for AI enhancement failures
- Use appropriate cancellation tokens for different scenarios
- Implement proper logging and monitoring
- Consider caching for frequently enhanced content
- Monitor API usage and costs
- Handle background jobs appropriately for your use case

For more information, refer to the `GeminiService.cs` implementation and the `TelegramUserApiClient.cs` integration examples. 