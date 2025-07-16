# 🚀 Hangfire-Enabled Gemini Service

This document explains how to use the enhanced Gemini service with Hangfire background job processing for improved performance and scalability.

## ✨ Features

- **Background Processing**: All Gemini API calls are now processed as background jobs
- **Job Tracking**: Each job gets a unique ID for tracking and result retrieval
- **Batch Processing**: Support for processing multiple messages in parallel
- **Automatic Retries**: Hangfire handles job retries automatically
- **Real-time Status**: Check job status and retrieve results
- **Enhanced Admin Logging**: Beautiful, detailed logging for monitoring

## 🛠️ Setup Requirements

### 1. Hangfire Configuration
Make sure Hangfire is configured in your `Program.cs`:

```csharp
// Add Hangfire services
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("HangfireConnection")));

// Add Hangfire server
builder.Services.AddHangfireServer();
```

### 2. Database Connection
Ensure you have a connection string for Hangfire in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "HangfireConnection": "Server=localhost;Database=HangfireDB;Trusted_Connection=true;"
  }
}
```

## 📡 API Endpoints

### 1. Enqueue Single Job
```http
POST /api/gemini/enhance
Content-Type: application/json

{
  "text": "Hello, please enhance this message",
  "apiKeyName": "optional-api-key-name"
}
```

**Response:**
```json
{
  "jobId": "a1b2c3d4e5f6",
  "status": "ENQUEUED",
  "timestamp": "2024-01-15T10:30:45.123Z"
}
```

### 2. Get Job Result
```http
GET /api/gemini/job/{jobId}
```

**Response:**
```json
{
  "jobId": "a1b2c3d4e5f6",
  "status": "COMPLETED",
  "result": "Enhanced message content here...",
  "timestamp": "2024-01-15T10:30:46.345Z"
}
```

### 3. Batch Processing
```http
POST /api/gemini/enhance/batch
Content-Type: application/json

{
  "texts": [
    "First message to enhance",
    "Second message to enhance",
    "Third message to enhance"
  ],
  "apiKeyName": "optional-api-key-name"
}
```

**Response:**
```json
{
  "message": "Batch jobs enqueued successfully",
  "jobCount": 3,
  "jobIds": ["job1", "job2", "job3"]
}
```

### 4. Check Multiple Job Statuses
```http
POST /api/gemini/jobs/status
Content-Type: application/json

["job1", "job2", "job3"]
```

## 🔄 Job Status Values

- `ENQUEUED`: Job has been queued but not started
- `RUNNING`: Job is currently being processed
- `COMPLETED`: Job finished successfully with result
- `NOT_FOUND`: Job ID doesn't exist
- `FAILED`: Job failed (check logs for details)

## 📊 Admin Logging

The service now provides beautiful, detailed admin logs with:

- **Operation Details**: Job ID, duration, start/end times
- **Performance Metrics**: Success rate, average response time
- **Configuration Usage**: Which API configs were used
- **Detailed Trace**: Step-by-step execution log
- **Categorized Events**: Organized by operation type

### Sample Admin Log Output:
```
╔══════════════════════════════════════════════════════════════════════════════════════════════════════╗
║ 🎯 GEMINI SERVICE ADMIN REPORT                                                                        ║
╠══════════════════════════════════════════════════════════════════════════════════════════════════════╣
║ 📋 OPERATION DETAILS                                                                                                                  ║
║    • Operation: EnhanceMessage                                                                                                        ║
║    • Status: ✅ SUCCESS                                                                                                               ║
║    • Duration: 1.2s                                                                                                                   ║
║    • Started: 2024-01-15 10:30:45.123 UTC                                                             ║
║    • Ended: 2024-01-15 10:30:46.345 UTC                                                               ║
║    • CID: a1b2c3d4e5f6                                                                                                                ║
╠══════════════════════════════════════════════════════════════════════════════════════════════════════╣
║ 📊 PERFORMANCE METRICS                                                                                                                ║
║    • Total Operations: 8                                                                                                              ║
║    • Success Rate: 87.5% (7/8)                                                                                                        ║
║    • Average Response Time: 150ms                                                                                                     ║
║    • Configurations Used: 2                                                                                                           ║
╠══════════════════════════════════════════════════════════════════════════════════════════════════════╣
║ 🔑 CONFIGURATION USAGE                                                                                                               ║
║    • Config ID 1: 5 operations                                                                                                        ║
║    • Config ID 2: 3 operations                                                                                                        ║
╠══════════════════════════════════════════════════════════════════════════════════════════════════════╣
║ 🔍 DETAILED TRACE                                                                                                                     ║
║ 📂 HANGFIRE                                                                                                                           ║
║    🚀 +0ms           🚀 Hangfire job started for text enhancement. JobId: a1b2c3d4e5f6                ║
║ 📂 CACHE                                                                                                                              ║
║    ➡️ +5ms           Request content hash (text-only): GeminiContent:abc123...                                                        ║
║    ✅ +10ms          Fulfilled from cache (Idempotency).                                                                              ║
╠══════════════════════════════════════════════════════════════════════════════════════════════════════╣
║ 🏁 END OF REPORT                                                                                                                     ║
╚══════════════════════════════════════════════════════════════════════════════════════════════════════╝
```

## 🚀 Benefits

1. **Non-Blocking**: API calls return immediately with job ID
2. **Scalable**: Multiple jobs can run in parallel
3. **Reliable**: Automatic retries and error handling
4. **Monitorable**: Detailed logging and job tracking
5. **Batch Support**: Process multiple messages efficiently
6. **Real-time Updates**: Check job status anytime

## 🔧 Usage Examples

### C# Client Example
```csharp
// Enqueue a job
var response = await httpClient.PostAsJsonAsync("/api/gemini/enhance", new
{
    text = "Hello, enhance this message",
    apiKeyName = "my-api-key"
});

var jobResponse = await response.Content.ReadFromJsonAsync<JobResultResponse>();
var jobId = jobResponse.JobId;

// Poll for result
while (true)
{
    var result = await httpClient.GetFromJsonAsync<JobResultResponse>($"/api/gemini/job/{jobId}");
    
    if (result.Status == "COMPLETED")
    {
        Console.WriteLine($"Enhanced message: {result.Result}");
        break;
    }
    else if (result.Status == "FAILED")
    {
        Console.WriteLine("Job failed");
        break;
    }
    
    await Task.Delay(1000); // Wait 1 second before checking again
}
```

### JavaScript Client Example
```javascript
// Enqueue a job
const response = await fetch('/api/gemini/enhance', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
        text: 'Hello, enhance this message',
        apiKeyName: 'my-api-key'
    })
});

const jobResponse = await response.json();
const jobId = jobResponse.jobId;

// Poll for result
const checkResult = async () => {
    const result = await fetch(`/api/gemini/job/${jobId}`);
    const jobResult = await result.json();
    
    if (jobResult.status === 'COMPLETED') {
        console.log('Enhanced message:', jobResult.result);
    } else if (jobResult.status === 'FAILED') {
        console.log('Job failed');
    } else {
        setTimeout(checkResult, 1000); // Check again in 1 second
    }
};

checkResult();
```

## 🎯 Best Practices

1. **Job Polling**: Don't poll too frequently (1-2 second intervals are good)
2. **Batch Size**: Keep batch sizes reasonable (max 100 items)
3. **Error Handling**: Always handle job failures gracefully
4. **Monitoring**: Use the admin logs to monitor performance
5. **Caching**: Results are cached for 1 hour, use this for repeated requests

## 🔍 Monitoring

- **Hangfire Dashboard**: Access at `/hangfire` to monitor all jobs
- **Admin Logs**: Check your notification service for detailed logs
- **API Logs**: Standard application logs for API calls
- **Job Status**: Use the status endpoints to track progress

This implementation provides a fast, scalable, and reliable way to process Gemini API calls using Hangfire background jobs! 🚀 