using Application.Common.Interfaces;
using Domain.Entities;
using Hangfire;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Services
{
    public class GeminiService : IGeminiService
    {
        // Dependencies
        private readonly ILogger<GeminiService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMemoryCache _cache;

        #region State Management (In-Class Strategy)
        // Static state to be shared across all instances of this service.
        private static readonly ConcurrentDictionary<int, AiApiConfiguration> _configs = new();
        private static List<int> _orderedConfigIds = new();
        private static readonly SemaphoreSlim _configLock = new(1, 1);
        private const string CONFIG_REFRESH_LOCK_KEY = "GeminiService_ConfigRefreshLock";
        private static readonly TimeSpan CONFIG_CACHE_DURATION = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CONFIG_FAILURE_COOLDOWN = TimeSpan.FromSeconds(60);
        #endregion

        #region Resilience Policies (In-Class Strategy)
        // A registry to hold a specific circuit breaker FOR EACH config ID.
        private static readonly ConcurrentDictionary<string, AsyncCircuitBreakerPolicy<HttpResponseMessage>> _circuitBreakers = new();
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly AsyncTimeoutPolicy<HttpResponseMessage> _timeoutPolicy; // Made generic
        #endregion

        // Idempotency lock per request content
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _requestLocks = new();

        // Quota Constants
        private const int FREE_RPM = 15;
        private const int FREE_TPM = 250_000;
        private const int FREE_RPD = 1_000;

        // JSON options
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        // Default prompt template for message enhancement
        private const string DEFAULT_PROMPT_TEMPLATE = @"You are an expert financial content enhancer. Your task is to improve the given trading signal message to make it more professional, engaging, and informative while maintaining all the original trading information.

IMPORTANT RULES:
1. Keep ALL original trading data (prices, levels, symbols) exactly as provided
2. Add professional formatting and structure
3. Enhance the language to be more engaging and professional
4. Add relevant trading context or insights if appropriate
5. Use markdown formatting for better presentation
6. Keep the message concise but informative

Original message: {message}

Enhanced message:";

        public GeminiService(
            ILogger<GeminiService> logger,
            IHttpClientFactory httpClientFactory,
            IServiceProvider serviceProvider,
            IMemoryCache memoryCache)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("GeminiClient");
            _serviceProvider = serviceProvider;
            _cache = memoryCache;

            // General policies that apply to any call
            _timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(15, TimeoutStrategy.Pessimistic);

            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
                .WaitAndRetryAsync(2, attempt => TimeSpan.FromMilliseconds(200 * attempt),
                    onRetry: (resp, ts, attempt, ctx) =>
                    {
                        var configId = ctx.GetValueOrDefault("ConfigId", "N/A");
                        _logger.LogWarning(
                            "[Polly] Retrying call for ConfigId {ConfigId}. Attempt {Attempt}. Delay {Delay}ms. Reason: {Reason}",
                            configId, attempt, ts.TotalMilliseconds, resp.Exception?.Message ?? resp.Result.StatusCode.ToString());
                    });
        }

        public async Task<string?> EnhanceMessageAsync(string text, CancellationToken ct, string? apiKeyName = null)
        {
            // First, try to get an immediate result
            try
            {
                var result = await AttemptImmediateEnhancementAsync(text, apiKeyName, ct);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    _logger.LogInformation("Message enhanced immediately. Text length: {TextLength} -> {ResultLength}", 
                        text?.Length ?? 0, result?.Length ?? 0);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Immediate enhancement failed, falling back to background job");
            }

            // Fallback to background job if immediate enhancement fails
            var jobId = Guid.NewGuid().ToString("N");
            
            // Enqueue the job and return immediately
            var jobIdResult = BackgroundJob.Enqueue(() => 
                ProcessEnhanceMessageJobAsync(text, jobId, apiKeyName, ct));
            
            _logger.LogInformation("EnhanceMessage job enqueued. JobId: {JobId}, HangfireJobId: {HangfireJobId}", 
                jobId, jobIdResult);
            
            // Return a placeholder response - in real implementation, you might want to return the job ID
            // and have the client poll for results or use SignalR for real-time updates
            return $"Job enqueued successfully. JobId: {jobId}";
        }

        public async Task<string?> EnhanceMessageAsync(
            string? text,
            ICollection<byte[]>? images, // This parameter will now be effectively ignored by the core logic.
            CancellationToken ct,
            string? apiKeyName = null)
        {
            // For the overload with images, we'll also use Hangfire
            var jobId = Guid.NewGuid().ToString("N");
            
            var jobIdResult = BackgroundJob.Enqueue(() => 
                ProcessEnhanceMessageJobAsync(text, jobId, apiKeyName, ct));
            
            _logger.LogInformation("EnhanceMessage job enqueued (with images). JobId: {JobId}, HangfireJobId: {HangfireJobId}", 
                jobId, jobIdResult);
            
            return $"Job enqueued successfully. JobId: {jobId}";
        }

        /// <summary>
        /// Attempts immediate enhancement without using background jobs
        /// </summary>
        private async Task<string?> AttemptImmediateEnhancementAsync(string text, string? apiKeyName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var adminLogger = new AdminLogger("ImmediateEnhancement", Guid.NewGuid().ToString("N"));

            try
            {
                adminLogger.Info($"🚀 Attempting immediate enhancement for text length: {text.Length}", null, "IMMEDIATE");

                // Generate cache key based on text only
                string contentCacheKey = GenerateContentCacheKey(text, null);
                adminLogger.Info($"Request content hash: {contentCacheKey}", null, "CACHE");

                // Check cache first
                if (_cache.TryGetValue(contentCacheKey, out string? cachedResult))
                {
                    adminLogger.Success("Fulfilled from cache.", null, "CACHE");
                    return cachedResult;
                }

                // Get configuration
                var config = await GetNextActiveConfigAsync(apiKeyName, adminLogger, ct);
                if (config == null)
                {
                    adminLogger.Failure("No available API configurations found.", null, "CONFIG");
                    return null;
                }

                adminLogger.Info($"Using Config ID {config.Id} (Model: {config.ModelName})", config.Id, "API_CALL");

                // Attempt API call
                (string? result, bool wasSuccess) = await AttemptApiCallAsync(config, text, null, adminLogger, ct);

                if (wasSuccess && !string.IsNullOrWhiteSpace(result))
                {
                    adminLogger.Success($"Successfully received response from Config ID {config.Id}.", config.Id, "API_CALL");
                    _cache.Set(contentCacheKey, result, TimeSpan.FromHours(1));
                    return result;
                }

                adminLogger.Failure($"Config ID {config.Id} failed.", config.Id, "API_CALL");
                return null;
            }
            catch (Exception ex)
            {
                adminLogger.Failure($"Exception during immediate enhancement: {ex.Message}", null, "EXCEPTION");
                _logger.LogError(ex, "Exception during immediate enhancement");
                return null;
            }
        }

        /// <summary>
        /// Hangfire background job method for processing message enhancement
        /// </summary>
        [AutomaticRetry(Attempts = 2)]
        public async Task ProcessEnhanceMessageJobAsync(string? text, string jobId, string? apiKeyName, CancellationToken ct)
        {
            var adminLogger = new AdminLogger("EnhanceMessage", jobId);

            try
            {
                adminLogger.Info($"🚀 Hangfire job started for text enhancement. JobId: {jobId}", null, "HANGFIRE");

                // 1. Input Validation & Idempotency
                if (string.IsNullOrWhiteSpace(text))
                {
                    adminLogger.Failure("Aborted: Text was null or empty.", null, "VALIDATION");
                    await NotifyAdminAsync(adminLogger.Render(), ct);
                    return;
                }

                // Generate cache key based on text only
                string contentCacheKey = GenerateContentCacheKey(text, null);
                adminLogger.Info($"Request content hash (text-only): {contentCacheKey}", null, "CACHE");

                var requestLock = _requestLocks.GetOrAdd(contentCacheKey, _ => new SemaphoreSlim(1, 1));
                await requestLock.WaitAsync(ct);

                try
                {
                    if (_cache.TryGetValue(contentCacheKey, out string? cachedResult))
                    {
                        adminLogger.Success("Fulfilled from cache (Idempotency).", null, "CACHE");
                        _cache.Set($"JobResult_{jobId}", cachedResult, TimeSpan.FromMinutes(30));
                        await NotifyAdminAsync(adminLogger.Render(), ct);
                        return;
                    }
                    adminLogger.Info("Cache miss, proceeding to API call.", null, "CACHE");

                    // 2. Main Execution Loop: Try available configs in order
                    int maxAttempts = (_configs.Count > 0 ? _configs.Count : 1) + 1;
                    for (int attempt = 1; attempt <= maxAttempts; attempt++)
                    {
                        var config = await GetNextActiveConfigAsync(apiKeyName, adminLogger, ct);
                        if (config == null)
                        {
                            adminLogger.Failure("No available API configurations found. Halting operation.", null, "CONFIG");
                            break;
                        }

                        adminLogger.Info($"Attempt {attempt}: Trying with Config ID {config.Id} (Model: {config.ModelName})", config.Id, "API_CALL");

                        (string? result, bool wasSuccess) = await AttemptApiCallAsync(config, text, null, adminLogger, ct);

                        if (wasSuccess)
                        {
                            adminLogger.Success($"Successfully received response from Config ID {config.Id}.", config.Id, "API_CALL");
                            _cache.Set(contentCacheKey, result, TimeSpan.FromHours(1));
                            
                            // Store the result in cache with job ID for retrieval
                            _cache.Set($"JobResult_{jobId}", result, TimeSpan.FromMinutes(30));
                            adminLogger.Success($"Job completed successfully. Result stored with JobId: {jobId}", null, "HANGFIRE");
                            return;
                        }

                        adminLogger.Failure($"Config ID {config.Id} failed. Rotating to the back.", config.Id, "ROTATION");
                        await ReportFailureAndRotateAsync(config.Id);
                    }

                    adminLogger.Failure("All available API configurations failed.", null, "FINAL");
                    _cache.Set($"JobResult_{jobId}", "FAILED: All configurations failed", TimeSpan.FromMinutes(30));
                }
                finally
                {
                    requestLock.Release();
                    await NotifyAdminAsync(adminLogger.Render(), ct);
                }
            }
            catch (Exception ex)
            {
                adminLogger.Failure($"Exception in background job: {ex.Message}", null, "EXCEPTION");
                _cache.Set($"JobResult_{jobId}", $"FAILED: {ex.Message}", TimeSpan.FromMinutes(30));
                await NotifyAdminAsync(adminLogger.Render(), ct);
            }
        }

        public async Task<string?> GetJobResultAsync(string jobId, CancellationToken ct)
        {
            if (_cache.TryGetValue($"JobResult_{jobId}", out string? result))
            {
                return result;
            }
            
            // If not in cache, check if job is still running
            var jobState = JobStorage.Current.GetMonitoringApi().JobDetails(jobId);
            if (jobState != null)
            {
                return "JOB_RUNNING";
            }
            
            return "JOB_NOT_FOUND";
        }

        /// <summary>
        /// Enqueue a batch of message enhancements
        /// </summary>
        public async Task<List<string>> EnhanceMessagesBatchAsync(List<string> texts, CancellationToken ct, string? apiKeyName = null)
        {
            var jobIds = new List<string>();
            
            foreach (var text in texts)
            {
                var jobId = Guid.NewGuid().ToString("N");
                var hangfireJobId = BackgroundJob.Enqueue(() => 
                    ProcessEnhanceMessageJobAsync(text, jobId, apiKeyName, ct));
                
                jobIds.Add(jobId);
                _logger.LogInformation("Batch job enqueued. Text: {TextLength} chars, JobId: {JobId}", 
                    text?.Length ?? 0, jobId);
            }
            
            return jobIds;
        }

        #region Core Logic Helpers

        private async Task<(string? result, bool wasSuccess)> AttemptApiCallAsync(
            AiApiConfiguration cfg,
            string? text,
            ICollection<byte[]>? images,
            AdminLogger adminLogger,
            CancellationToken ct)
        {
            // Quota Check
            if (!CheckAndIncrementQuota(cfg.ModelName, text))
            {
                adminLogger.Info($"Quota exceeded for model {cfg.ModelName}; skipping.", cfg.Id, "QUOTA");
                return (null, false);
            }

            // Get the specific circuit breaker for this key
            var circuitBreaker = GetOrCreateCircuitBreaker(cfg.Id, adminLogger);
            if (circuitBreaker.CircuitState == CircuitState.Open)
            {
                adminLogger.Info($"Circuit breaker is open for Config ID {cfg.Id}. Skipping.", cfg.Id, "CIRCUIT_BREAKER");
                return (null, false);
            }

            var policyWrap = Policy.WrapAsync<HttpResponseMessage>(_timeoutPolicy, circuitBreaker, _retryPolicy);
            var pollyCtx = new Context($"GeminiCall-{cfg.Id}", new Dictionary<string, object> { { "ConfigId", cfg.Id.ToString() } });

            try
            {
                // Use the configured prompt template or fall back to default
                var promptTemplate = !string.IsNullOrWhiteSpace(cfg.PromptTemplate) ? cfg.PromptTemplate : DEFAULT_PROMPT_TEMPLATE;
                var request = BuildRequest(promptTemplate, text);
                var uri = $"https://generativelanguage.googleapis.com/v1beta/models/{cfg.ModelName}:generateContent?key={cfg.ApiKey}";

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response = await policyWrap.ExecuteAsync(ctx => _httpClient.PostAsJsonAsync(uri, request, _jsonOpts, ct), pollyCtx);
                sw.Stop();

                adminLogger.Info($"HTTP POST returned {(int)response.StatusCode} {response.ReasonPhrase} in {sw.ElapsedMilliseconds}ms.", cfg.Id, "API_CALL");

                if (response.IsSuccessStatusCode)
                {
                    var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
                    var responseText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.parts?.FirstOrDefault()?.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(responseText))
                    {
                        return (responseText, true);
                    }
                    adminLogger.Failure("API returned success status but content was empty.", cfg.Id, "API_RESPONSE");
                    return (null, false);
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    adminLogger.Failure("API returned 429 TooManyRequests. This key will be on cooldown.", cfg.Id, "QUOTA");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    adminLogger.Failure($"API returned non-success status: {(int)response.StatusCode}. Error: {errorContent.Truncate(100)}", cfg.Id, "API_RESPONSE");
                }

                return (null, false);
            }
            catch (BrokenCircuitException)
            {
                adminLogger.Failure("Circuit breaker tripped and is now open.", cfg.Id, "CIRCUIT_BREAKER");
                return (null, false);
            }
            catch (TimeoutRejectedException)
            {
                adminLogger.Failure("Request timed out by Polly policy.", cfg.Id, "TIMEOUT");
                return (null, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during API call for Config ID {ConfigId}", cfg.Id);
                adminLogger.Failure($"Network/HTTP exception: {ex.Message}", cfg.Id, "NETWORK");
                return (null, false);
            }
        }

        private async Task<AiApiConfiguration?> GetNextActiveConfigAsync(string? apiKeyName, AdminLogger adminLogger, CancellationToken ct)
        {
            await RefreshConfigsIfNeededAsync(adminLogger, ct);

            await _configLock.WaitAsync(ct);
            try
            {
                // Find the first ID in our ordered list that is not on cooldown.
                // NOTE: Filtering by 'apiKeyName' was removed because the property 'KeyName' does not exist on your AiApiConfiguration entity.
                // If you add this property to your entity, you can re-enable the commented-out filter.
                var configId = _orderedConfigIds.FirstOrDefault(id =>
                {
                    if (!_configs.TryGetValue(id, out var conf)) return false;
                    //bool nameMatch = string.IsNullOrEmpty(apiKeyName) || conf.KeyName == apiKeyName; // <-- This line requires 'KeyName' property
                    bool onCooldown = _cache.TryGetValue(GetCooldownCacheKey(id), out _);
                    return !onCooldown; // && nameMatch;
                });

                return configId != 0 && _configs.TryGetValue(configId, out var config) ? config : null;
            }
            finally
            {
                _configLock.Release();
            }
        }

        private async Task ReportFailureAndRotateAsync(int failedConfigId)
        {
            _cache.Set(GetCooldownCacheKey(failedConfigId), true, CONFIG_FAILURE_COOLDOWN);

            await _configLock.WaitAsync();
            try
            {
                if (_orderedConfigIds.Remove(failedConfigId))
                {
                    _orderedConfigIds.Add(failedConfigId);
                }
            }
            finally
            {
                _configLock.Release();
            }
        }

        private async Task RefreshConfigsIfNeededAsync(AdminLogger adminLogger, CancellationToken ct)
        {
            if (!_cache.TryGetValue(CONFIG_REFRESH_LOCK_KEY, out _))
            {
                await _configLock.WaitAsync(ct);
                try
                {
                    if (_cache.TryGetValue(CONFIG_REFRESH_LOCK_KEY, out _)) return;

                    adminLogger.Info("Config cache expired. Refreshing configurations from database.", null, "CONFIG_REFRESH");

                    await using var scope = _serviceProvider.CreateAsyncScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IAiApiConfigurationRepository>();
                    var allDbConfigs = await repo.GetAllByProviderAndStatusAsync("Gemini", true, ct);

                    var freshConfigs = allDbConfigs
                        .Where(c => !string.IsNullOrWhiteSpace(c.ApiKey) && !string.IsNullOrWhiteSpace(c.ModelName))
                        .ToList();

                    _configs.Clear();
                    foreach (var cfg in freshConfigs)
                    {
                        _configs[cfg.Id] = cfg;
                    }

                    var newOrder = _orderedConfigIds.Intersect(_configs.Keys).ToList();
                    var addedKeys = _configs.Keys.Except(newOrder).ToList();
                    newOrder.AddRange(addedKeys);
                    _orderedConfigIds = newOrder;

                    adminLogger.Info($"Refresh complete. Found {_configs.Count} active configs. Order: [{string.Join(",", _orderedConfigIds)}]", null, "CONFIG_REFRESH");
                    _cache.Set(CONFIG_REFRESH_LOCK_KEY, true, CONFIG_CACHE_DURATION);
                }
                finally
                {
                    _configLock.Release();
                }
            }
        }

        private AsyncCircuitBreakerPolicy<HttpResponseMessage> GetOrCreateCircuitBreaker(int configId, AdminLogger adminLogger)
        {
            var key = $"GeminiConfig_{configId}";
            return _circuitBreakers.GetOrAdd(key, _ =>
            {
                adminLogger.Info($"Creating new circuit breaker for Config ID {configId}.", configId, "CIRCUIT_BREAKER");
                return Policy<HttpResponseMessage>
                    .Handle<HttpRequestException>()
                    .OrResult(r => (int)r.StatusCode >= 500)
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking: 3,
                        durationOfBreak: TimeSpan.FromMinutes(2),
                        onBreak: (outcome, ts) => adminLogger.Failure($"Circuit breaker opened for Config ID {configId}. Break duration: {ts.TotalMinutes} minutes.", configId, "CIRCUIT_BREAKER"),
                        onReset: () => adminLogger.Info($"Circuit breaker reset for Config ID {configId}.", configId, "CIRCUIT_BREAKER"),
                        onHalfOpen: () => adminLogger.Info($"Circuit breaker half-open for Config ID {configId}.", configId, "CIRCUIT_BREAKER")
                    );
            });
        }

        private bool CheckAndIncrementQuota(string model, string? text)
        {
            var now = DateTime.UtcNow;
            var minuteKey = $"quota_rpm_{model}_{now:yyyyMMddHHmm}";
            var dayKey = $"quota_rpd_{model}_{now:yyyyMMdd}";
            var tokenKey = $"quota_tpm_{model}_{now:yyyyMMddHHmm}";

            // Check RPM
            var currentRpm = _cache.GetOrCreate(minuteKey, entry => { entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1); return 0; });
            if (currentRpm >= FREE_RPM) return false;
            _cache.Set(minuteKey, currentRpm + 1, TimeSpan.FromMinutes(1));

            // Check RPD
            var currentRpd = _cache.GetOrCreate(dayKey, entry => { entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1); return 0; });
            if (currentRpd >= FREE_RPD) return false;
            _cache.Set(dayKey, currentRpd + 1, TimeSpan.FromDays(1));

            // Check TPM (rough estimate: 1 token ≈ 4 characters)
            var estimatedTokens = (text?.Length ?? 0) / 4;
            var currentTpm = _cache.GetOrCreate(tokenKey, entry => { entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1); return 0; });
            if (currentTpm + estimatedTokens > FREE_TPM) return false;
            _cache.Set(tokenKey, currentTpm + estimatedTokens, TimeSpan.FromMinutes(1));

            return true;
        }

        private static string GenerateContentCacheKey(string? text, ICollection<byte[]>? images)
        {
            var content = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(text))
            {
                content.Append($"text:{text}");
            }
            if (images != null && images.Any())
            {
                foreach (var img in images)
                {
                    using var sha256 = SHA256.Create();
                    var hash = sha256.ComputeHash(img);
                    content.Append($"img:{Convert.ToBase64String(hash)}");
                }
            }
            using var finalHash = SHA256.Create();
            var finalBytes = finalHash.ComputeHash(Encoding.UTF8.GetBytes(content.ToString()));
            return $"gemini_content_{Convert.ToBase64String(finalBytes)}";
        }
        #endregion

        private string GetCooldownCacheKey(int configId) => $"GeminiConfig_Cooldown_{configId}";

        private async Task NotifyAdminAsync(string message, CancellationToken ct)
        {
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var notificationService = scope.ServiceProvider.GetService<INotificationToAdminService>();
                if (notificationService != null)
                {
                    await notificationService.SendNotificationAsync(message, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send admin notification");
            }
        }

        private GeminiRequest BuildRequest(string promptTemplate, string? text)
        {
            var parts = new List<Part>();

            string processedText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Apply the template to the provided text.
                processedText = promptTemplate.Replace("{message}", text);
            }
            else
            {
                // If text is null or empty, use the template with an empty message,
                // or just the template itself if it doesn't require a message.
                processedText = promptTemplate.Replace("{message}", "").Trim();
            }

            // Add the single text part.
            if (!string.IsNullOrWhiteSpace(processedText))
            {
                parts.Add(new Part(Text: processedText, InlineData: null));
            }
            return new GeminiRequest(new List<Content> { new(parts) });
        }

        public record GeminiRequest(List<Content> contents);
        public record Content(List<Part> parts);
        public record Part([property: JsonPropertyName("text")] string? Text, [property: JsonPropertyName("inline_data")] InlineData? InlineData);
        public record InlineData([property: JsonPropertyName("mime_type")] string MimeType, [property: JsonPropertyName("data")] string Data);
        public record GeminiResponse([property: JsonPropertyName("candidates")] List<Candidate>? Candidates);
        public record Candidate([property: JsonPropertyName("content")] Content? Content);

        private class AdminLogger
        {
            private readonly string _operationName;
            private readonly Stopwatch _totalStopwatch;
            private readonly List<LogEntry> _entries = new();
            private string? _finalStatus;
            private readonly DateTime _startTime;
            private int _successCount = 0;
            private int _failureCount = 0;
            private int _infoCount = 0;
            private readonly Dictionary<int, int> _configUsageCount = new();

            public string CorrelationId { get; }

            public AdminLogger(string operationName, string correlationId)
            {
                _operationName = operationName;
                CorrelationId = correlationId;
                _totalStopwatch = Stopwatch.StartNew();
                _startTime = DateTime.UtcNow;
                Info($"🚀 Operation '{_operationName}' Started", null, "START");
            }

            public void Info(string message, int? configId = null, string? category = null) 
            {
                _entries.Add(new LogEntry(message, _totalStopwatch.Elapsed, configId, "INFO", category));
                _infoCount++;
                if (configId.HasValue)
                    _configUsageCount[configId.Value] = _configUsageCount.GetValueOrDefault(configId.Value) + 1;
            }

            public void Success(string message, int? configId = null, string? category = null)
            {
                _entries.Add(new LogEntry(message, _totalStopwatch.Elapsed, configId, "SUCCESS", category));
                _successCount++;
                if (configId.HasValue)
                    _configUsageCount[configId.Value] = _configUsageCount.GetValueOrDefault(configId.Value) + 1;
                _finalStatus = "✅ SUCCESS";
            }

            public void Failure(string message, int? configId = null, string? category = null)
            {
                _entries.Add(new LogEntry(message, _totalStopwatch.Elapsed, configId, "FAILURE", category));
                _failureCount++;
                if (configId.HasValue)
                    _configUsageCount[configId.Value] = _configUsageCount.GetValueOrDefault(configId.Value) + 1;
                if (_finalStatus == null) _finalStatus = "❌ FAILURE";
            }

            public string Render()
            {
                _totalStopwatch.Stop();
                var sb = new StringBuilder();
                
                // Enhanced Header with gradient-like effect
                sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║ 🎯 GEMINI SERVICE ADMIN REPORT                                                                        ║");
                sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════════════════════════════╣");
                
                // Operation Details Section
                sb.AppendLine("║ 📋 OPERATION DETAILS                                                                                                                  ║");
                sb.AppendLine($"║    • Operation: {_operationName,-60} ║");
                sb.AppendLine($"║    • Status: {GetStatusWithColor(_finalStatus ?? "UNKNOWN"),-65} ║");
                sb.AppendLine($"║    • Duration: {FormatDuration(_totalStopwatch.Elapsed),-60} ║");
                sb.AppendLine($"║    • Started: {_startTime:yyyy-MM-dd HH:mm:ss.fff} UTC                                                      ║");
                sb.AppendLine($"║    • Ended: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC                                                        ║");
                sb.AppendLine($"║    • CID: {CorrelationId,-65} ║");
                
                // Performance Metrics Section
                sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════════════════════════════╣");
                sb.AppendLine("║ 📊 PERFORMANCE METRICS                                                                                                                ║");
                sb.AppendLine($"║    • Total Operations: {_entries.Count,-55} ║");
                sb.AppendLine($"║    • Success Rate: {CalculateSuccessRate(),-60} ║");
                sb.AppendLine($"║    • Average Response Time: {CalculateAverageResponseTime(),-50} ║");
                sb.AppendLine($"║    • Configurations Used: {_configUsageCount.Count,-55} ║");
                
                // Configuration Usage Summary
                if (_configUsageCount.Any())
                {
                    sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════════════════════════════╣");
                    sb.AppendLine("║ 🔑 CONFIGURATION USAGE                                                                                                               ║");
                    foreach (var kvp in _configUsageCount.OrderByDescending(x => x.Value))
                    {
                        sb.AppendLine($"║    • Config ID {kvp.Key}: {kvp.Value} operations                                                          ║");
                    }
                }
                
                // Detailed Trace Section
                sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════════════════════════════╣");
                sb.AppendLine("║ 🔍 DETAILED TRACE                                                                                                                     ║");
                
                var groupedEntries = _entries.GroupBy(e => e.Category ?? "GENERAL").OrderBy(g => g.Key);
                
                foreach (var group in groupedEntries)
                {
                    if (group.Key != "GENERAL")
                    {
                        sb.AppendLine($"║ 📂 {group.Key.ToUpper()}                                                                                                                ║");
                    }
                    
                    foreach (var entry in group)
                    {
                        string icon = GetStatusIcon(entry.Status);
                        string configInfo = entry.ConfigId.HasValue ? $"[Config:{entry.ConfigId}] " : "";
                        string timestamp = $"+{entry.Timestamp.TotalMilliseconds:F0}ms";
                        string message = TruncateMessage(entry.Message, 70);
                        
                        sb.AppendLine($"║    {icon} {timestamp,-10} {configInfo,-12} {message,-70} ║");
                    }
                    
                    if (group.Key != "GENERAL" && group != groupedEntries.Last())
                    {
                        sb.AppendLine("║                                                                                                                              ║");
                    }
                }
                
                // Footer
                sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════════════════════════════╣");
                sb.AppendLine("║ 🏁 END OF REPORT                                                                                                                     ║");
                sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════════════════════════════╝");
                
                return sb.ToString();
            }

            private string GetStatusWithColor(string status)
            {
                return status switch
                {
                    "✅ SUCCESS" => "✅ SUCCESS",
                    "❌ FAILURE" => "❌ FAILURE",
                    _ => "❓ UNKNOWN"
                };
            }

            private string GetStatusIcon(string status)
            {
                return status switch
                {
                    "SUCCESS" => "✅",
                    "FAILURE" => "❌",
                    "START" => "🚀",
                    _ => "➡️"
                };
            }

            private string FormatDuration(TimeSpan duration)
            {
                if (duration.TotalMilliseconds < 1000)
                    return $"{duration.TotalMilliseconds:F0}ms";
                else if (duration.TotalSeconds < 60)
                    return $"{duration.TotalSeconds:F1}s";
                else
                    return $"{duration.TotalMinutes:F1}m {duration.Seconds}s";
            }

            private string CalculateSuccessRate()
            {
                if (_entries.Count == 0) return "0%";
                var successRate = (double)_successCount / _entries.Count * 100;
                return $"{successRate:F1}% ({_successCount}/{_entries.Count})";
            }

            private string CalculateAverageResponseTime()
            {
                if (_entries.Count == 0) return "0ms";
                var avgTime = _entries.Average(e => e.Timestamp.TotalMilliseconds);
                return $"{avgTime:F0}ms";
            }

            private string TruncateMessage(string message, int maxLength)
            {
                return message.Length <= maxLength ? message : message[..maxLength] + "...";
            }

            private record LogEntry(string Message, TimeSpan Timestamp, int? ConfigId, string Status, string? Category);
        }
    }

    public static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value[..maxLength] + "...";
        }
    }
}