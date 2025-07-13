# GeminiService Troubleshooting Guide

## Issue: Messages Are Not Being Enhanced

If your messages are not being enhanced by the GeminiService, follow this troubleshooting guide to identify and fix the problem.

## 🔍 Step-by-Step Diagnosis

### 1. Check Database Configuration

**Problem**: No Gemini configuration exists in the database.

**Solution**: Add a default Gemini configuration:

```sql
-- Run this SQL script in your database
INSERT INTO "AiApiConfigurations" (
    "ProviderName",
    "IsEnabled", 
    "ApiKey",
    "ModelName",
    "PromptTemplate",
    "Description",
    "ApiKeyName",
    "CreatedAt",
    "LastUpdatedAt"
) VALUES (
    'Gemini',
    true,
    'YOUR_GEMINI_API_KEY_HERE', -- Replace with your actual API key
    'gemini-1.5-flash-latest',
    'You are an expert financial content enhancer. Your task is to improve the given trading signal message to make it more professional, engaging, and informative while maintaining all the original trading information.

IMPORTANT RULES:
1. Keep ALL original trading data (prices, levels, symbols) exactly as provided
2. Add professional formatting and structure
3. Enhance the language to be more engaging and professional
4. Add relevant trading context or insights if appropriate
5. Use markdown formatting for better presentation
6. Keep the message concise but informative

Original message: {message}

Enhanced message:',
    'Default Gemini configuration for message enhancement',
    'Default',
    NOW(),
    NOW()
);
```

### 2. Verify API Key

**Problem**: Invalid or missing Gemini API key.

**Solution**: 
1. Get a valid API key from [Google AI Studio](https://makersuite.google.com/app/apikey)
2. Update the configuration in the database:

```sql
UPDATE "AiApiConfigurations" 
SET "ApiKey" = 'your-actual-api-key-here'
WHERE "ProviderName" = 'Gemini';
```

### 3. Check Service Registration

**Problem**: GeminiService not properly registered in DI container.

**Solution**: Ensure the service is registered in `Program.cs`:

```csharp
// In Program.cs
builder.Services.AddSingleton<IGeminiService, GeminiService>();
```

### 4. Verify HttpClient Configuration

**Problem**: Missing HttpClient configuration for Gemini.

**Solution**: Add HttpClient configuration:

```csharp
// In Program.cs
builder.Services.AddHttpClient("GeminiClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "ForexTradingBot/1.0");
});
```

### 5. Check Logs for Errors

**Problem**: Service is failing silently.

**Solution**: Check application logs for:
- Configuration errors
- API call failures
- Quota exceeded messages
- Circuit breaker trips

Look for log entries containing:
- `GeminiService`
- `EnhanceMessage`
- `API_CALL`
- `CONFIG_REFRESH`

### 6. Test Immediate Enhancement

**Problem**: Service is only using background jobs.

**Solution**: The service now supports immediate enhancement. Check if you're getting immediate results or job IDs:

```csharp
// This should return enhanced text immediately if possible
var result = await _geminiService.EnhanceMessageAsync(message, cancellationToken);

if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Job enqueued"))
{
    // Immediate enhancement succeeded
    Console.WriteLine($"Enhanced: {result}");
}
else if (!string.IsNullOrWhiteSpace(result) && result.StartsWith("Job enqueued"))
{
    // Background job was enqueued
    var jobId = result.Replace("Job enqueued successfully. JobId: ", "");
    Console.WriteLine($"Job enqueued: {jobId}");
    
    // Check job result
    var jobResult = await _geminiService.GetJobResultAsync(jobId, cancellationToken);
    Console.WriteLine($"Job result: {jobResult}");
}
```

## 🛠️ Common Fixes

### Fix 1: Enable Immediate Enhancement

The service now tries immediate enhancement first. If it fails, it falls back to background jobs. This should resolve the issue where messages weren't being enhanced.

### Fix 2: Add Default Prompt Template

The service includes a default prompt template that will be used if no template is configured in the database:

```csharp
private const string DEFAULT_PROMPT_TEMPLATE = @"You are an expert financial content enhancer...";
```

### Fix 3: Check Configuration Cache

The service caches configurations for 5 minutes. If you update the database, wait up to 5 minutes or restart the application:

```csharp
// Force refresh configurations
await RefreshConfigsIfNeededAsync(adminLogger, ct);
```

### Fix 4: Verify Model Name

Ensure the model name is correct:

```sql
-- Check current model
SELECT "ModelName" FROM "AiApiConfigurations" WHERE "ProviderName" = 'Gemini';

-- Update to latest model if needed
UPDATE "AiApiConfigurations" 
SET "ModelName" = 'gemini-1.5-flash-latest'
WHERE "ProviderName" = 'Gemini';
```

## 🔧 Testing the Service

### Test 1: Direct Service Test

```csharp
// Create a test method
public async Task TestGeminiService()
{
    var geminiService = serviceProvider.GetRequiredService<IGeminiService>();
    
    var testMessage = @"GOLD SELL NOW 
3295 - 3297

SL : 3300

TP1: 3293
TP2: 3291
TP3: 3289";

    var result = await geminiService.EnhanceMessageAsync(testMessage, CancellationToken.None);
    
    Console.WriteLine($"Result: {result}");
}
```

### Test 2: Check Job Results

```csharp
// If you get a job ID, check the result
var jobId = "your-job-id-here";
var result = await _geminiService.GetJobResultAsync(jobId, CancellationToken.None);

switch (result)
{
    case "JOB_RUNNING":
        Console.WriteLine("Job is still running");
        break;
    case "JOB_NOT_FOUND":
        Console.WriteLine("Job not found");
        break;
    default:
        Console.WriteLine($"Job completed: {result}");
        break;
}
```

## 📊 Monitoring and Debugging

### Enable Debug Logging

Add this to your `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Application.Services.GeminiService": "Debug"
    }
  }
}
```

### Check Admin Notifications

The service sends detailed admin notifications. Check if you're receiving them in your admin panel.

### Monitor Cache

The service uses caching for:
- Configurations (5 minutes)
- Enhanced content (1 hour)
- Job results (30 minutes)

Clear cache if needed:

```csharp
// Clear all Gemini-related cache
_cache.Remove("GeminiService_ConfigRefreshLock");
```

## 🚨 Emergency Fixes

### Quick Fix: Restart Application

If all else fails, restart the application to clear caches and reload configurations.

### Manual Configuration Check

```sql
-- Check if configuration exists and is enabled
SELECT 
    "Id",
    "ProviderName", 
    "IsEnabled",
    "ModelName",
    "ApiKeyName",
    CASE 
        WHEN "ApiKey" IS NOT NULL AND LENGTH("ApiKey") > 10 THEN 'Configured'
        ELSE 'Missing API Key'
    END as "ApiKeyStatus"
FROM "AiApiConfigurations" 
WHERE "ProviderName" = 'Gemini';
```

### Force Configuration Refresh

```csharp
// In your application, force a configuration refresh
var geminiService = serviceProvider.GetRequiredService<IGeminiService>();
// The service will automatically refresh configurations on next use
```

## ✅ Success Indicators

When the service is working correctly, you should see:

1. **Immediate Enhancement**: Messages are enhanced right away without job IDs
2. **Professional Output**: Enhanced messages have better formatting and structure
3. **Preserved Data**: All trading data (prices, levels) remains intact
4. **Markdown Formatting**: Enhanced messages use proper markdown formatting
5. **Admin Logs**: Detailed admin notifications about the enhancement process

## 📞 Getting Help

If you're still experiencing issues:

1. Check the application logs for detailed error messages
2. Verify your Gemini API key is valid and has sufficient quota
3. Ensure the database configuration is correct
4. Test with a simple message first
5. Check if the service is properly registered in your DI container

The GeminiService is designed to be robust and provide both immediate and background processing options. With proper configuration, it should enhance your trading messages effectively. 