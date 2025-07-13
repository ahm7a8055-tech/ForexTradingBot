using Application.Common.Interfaces;
using Domain.Entities;
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

        public Task<string?> EnhanceMessageAsync(string text, CancellationToken ct, string? apiKeyName = null)
            => EnhanceMessageAsync(text, null, ct, apiKeyName);

        public async Task<string?> EnhanceMessageAsync(
            string? text,
            ICollection<byte[]>? images,
            CancellationToken ct,
            string? apiKeyName = null)
        {
            var adminLogger = new AdminLogger("EnhanceMessage", Guid.NewGuid().ToString("N"));

            // 1. Input Validation & Idempotency
            if (string.IsNullOrWhiteSpace(text) && (images == null || !images.Any()))
            {
                adminLogger.Failure("Aborted: Text and images were both null or empty.");
                await NotifyAdminAsync(adminLogger.Render(), ct);
                return null;
            }

            string contentCacheKey = GenerateContentCacheKey(text, images);
            adminLogger.Info($"Request content hash: {contentCacheKey}");

            var requestLock = _requestLocks.GetOrAdd(contentCacheKey, _ => new SemaphoreSlim(1, 1));
            await requestLock.WaitAsync(ct);

            try
            {
                if (_cache.TryGetValue(contentCacheKey, out string? cachedResult))
                {
                    adminLogger.Success("Fulfilled from cache (Idempotency).");
                    await NotifyAdminAsync(adminLogger.Render(), ct);
                    return cachedResult;
                }
                adminLogger.Info("Cache miss, proceeding to API call.");

                // 2. Main Execution Loop: Try available configs in order
                int maxAttempts = (_configs.Count > 0 ? _configs.Count : 1) + 1; // Failsafe loop
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    var config = await GetNextActiveConfigAsync(apiKeyName, adminLogger, ct);
                    if (config == null)
                    {
                        adminLogger.Failure("No available API configurations found. Halting operation.");
                        break; // Exit loop if no configs are available
                    }

                    adminLogger.Info($"Attempt {attempt}: Trying with Config ID {config.Id} (Model: {config.ModelName})");

                    // The core attempt to call the API with the selected config
                    (string? result, bool wasSuccess) = await AttemptApiCallAsync(config, text, images, adminLogger, ct);

                    if (wasSuccess)
                    {
                        adminLogger.Success($"Successfully received response from Config ID {config.Id}.", config.Id);
                        _cache.Set(contentCacheKey, result, TimeSpan.FromHours(1));
                        return result; // Success, exit the method
                    }

                    // If the call was not successful, rotate the failed key to the back of the line
                    adminLogger.Failure($"Config ID {config.Id} failed. Rotating to the back.", config.Id);
                    await ReportFailureAndRotateAsync(config.Id);
                }

                adminLogger.Failure("All available API configurations failed.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in EnhanceMessageAsync. CID: {CID}", adminLogger.CorrelationId);
                adminLogger.Failure($"CRITICAL ERROR: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
            finally
            {
                requestLock.Release();
                await NotifyAdminAsync(adminLogger.Render(), ct);
            }
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
                adminLogger.Info($"Quota exceeded for model {cfg.ModelName}; skipping.", cfg.Id);
                return (null, false);
            }

            // Get the specific circuit breaker for this key
            var circuitBreaker = GetOrCreateCircuitBreaker(cfg.Id, adminLogger);
            if (circuitBreaker.CircuitState == CircuitState.Open)
            {
                adminLogger.Info($"Circuit breaker is open for Config ID {cfg.Id}. Skipping.", cfg.Id);
                return (null, false);
            }

            var policyWrap = Policy.WrapAsync<HttpResponseMessage>(_timeoutPolicy, circuitBreaker, _retryPolicy);
            var pollyCtx = new Context($"GeminiCall-{cfg.Id}", new Dictionary<string, object> { { "ConfigId", cfg.Id.ToString() } });

            try
            {
                var request = BuildRequest(cfg.PromptTemplate, text, images);
                var uri = $"https://generativelanguage.googleapis.com/v1beta/models/{cfg.ModelName}:generateContent?key={cfg.ApiKey}";

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response = await policyWrap.ExecuteAsync(ctx => _httpClient.PostAsJsonAsync(uri, request, _jsonOpts, ct), pollyCtx);
                sw.Stop();

                adminLogger.Info($"HTTP POST returned {(int)response.StatusCode} {response.ReasonPhrase} in {sw.ElapsedMilliseconds}ms.", cfg.Id);

                if (response.IsSuccessStatusCode)
                {
                    var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
                    var responseText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.parts?.FirstOrDefault()?.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(responseText))
                    {
                        return (responseText, true);
                    }
                    adminLogger.Failure("API returned success status but content was empty.", cfg.Id);
                    return (null, false);
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    adminLogger.Failure("API returned 429 TooManyRequests. This key will be on cooldown.", cfg.Id);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    adminLogger.Failure($"API returned non-success status: {(int)response.StatusCode}. Error: {errorContent.Truncate(100)}", cfg.Id);
                }

                return (null, false);
            }
            catch (BrokenCircuitException)
            {
                adminLogger.Failure("Circuit breaker tripped and is now open.", cfg.Id);
                return (null, false);
            }
            catch (TimeoutRejectedException)
            {
                adminLogger.Failure("Request timed out by Polly policy.", cfg.Id);
                return (null, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during API call for Config ID {ConfigId}", cfg.Id);
                adminLogger.Failure($"Network/HTTP exception: {ex.Message}", cfg.Id);
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

                    adminLogger.Info("Config cache expired. Refreshing configurations from database.");

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

                    adminLogger.Info($"Refresh complete. Found {_configs.Count} active configs. Order: [{string.Join(",", _orderedConfigIds)}]");
                    _cache.Set(CONFIG_REFRESH_LOCK_KEY, true, CONFIG_CACHE_DURATION);
                }
                finally
                {
                    _configLock.Release();
                }
            }
        }

        #endregion

        #region Policies & Utilities

        private AsyncCircuitBreakerPolicy<HttpResponseMessage> GetOrCreateCircuitBreaker(int configId, AdminLogger adminLogger)
        {
            return _circuitBreakers.GetOrAdd($"ConfigId_{configId}", _ =>
            {
                adminLogger.Info($"Creating new circuit breaker for Config ID {configId}.");
                return Policy<HttpResponseMessage>
                    .Handle<HttpRequestException>()
                    .OrResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.BadRequest)
                    .CircuitBreakerAsync(
                        2, TimeSpan.FromMinutes(2),
                        onBreak: (resp, ts, ctx) => _logger.LogWarning("[Polly] Circuit breaker for ConfigId {ConfigId} is now open for {BreakTime}s due to: {Reason}", configId, ts.TotalSeconds, resp.Exception?.Message ?? resp.Result.StatusCode.ToString()),
                        onReset: (ctx) => _logger.LogInformation("[Polly] Circuit breaker for ConfigId {ConfigId} has been reset.", configId),
                        onHalfOpen: () => _logger.LogInformation("[Polly] Circuit breaker for ConfigId {ConfigId} is now half-open. Next call is a test.", configId)
                    );
            });
        }


        private bool CheckAndIncrementQuota(string model, string? text)
        {
            var tokens = text?.Length / 4 ?? 1; // Rough approximation
            var rpmKey = $"Quota_RPM_{model}";
            var tpmKey = $"Quota_TPM_{model}";
            var rpdKey = $"Quota_RPD_{model}";

            var rpm = _cache.GetOrCreate(rpmKey, entry => { entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1); return 0; });
            var tpm = _cache.GetOrCreate(tpmKey, entry => { entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1); return 0; });
            var rpd = _cache.GetOrCreate(rpdKey, entry => { entry.AbsoluteExpiration = DateTime.Today.AddDays(1); return 0; });

            if (rpm >= FREE_RPM || tpm + tokens > FREE_TPM || rpd >= FREE_RPD) return false;

            _cache.Set(rpmKey, rpm + 1);
            _cache.Set(tpmKey, tpm + tokens);
            _cache.Set(rpdKey, rpd + 1);
            return true;
        }

        private static string GenerateContentCacheKey(string? text, ICollection<byte[]>? images)
        {
            using var sha = SHA256.Create();
            using var ms = new MemoryStream();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var b = Encoding.UTF8.GetBytes("TEXT:" + text);
                ms.Write(b, 0, b.Length);
            }
            if (images != null)
            {
                foreach (var img in images.OrderBy(i => i.Length))
                {
                    var hb = Encoding.UTF8.GetBytes($"IMG_LEN:{img.Length}:");
                    ms.Write(hb, 0, hb.Length);
                    ms.Write(img, 0, img.Length);
                }
            }
            if (ms.Length == 0) return "EMPTY_CONTENT";
            ms.Position = 0;
            var hash = sha.ComputeHash(ms);
            return $"GeminiContent:{Convert.ToBase64String(hash)}";
        }

        private string GetCooldownCacheKey(int configId) => $"GeminiConfig_Cooldown_{configId}";

        private async Task NotifyAdminAsync(string message, CancellationToken ct)
        {
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var notifier = scope.ServiceProvider.GetRequiredService<INotificationToAdminService>();
                await notifier.SendNotificationAsync(message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send admin notification.");
            }
        }
        #endregion

        #region DTOs and Request Builder

        // ====================================================================================
        // == START OF FIX: This method now correctly builds multimodal and text-only requests.
        // ====================================================================================
        private GeminiRequest BuildRequest(string promptTemplate, string? text, ICollection<byte[]>? images)
        {
            var parts = new List<Part>();
            bool hasImages = images?.Any() ?? false;

            // Determine the prompt text based on whether this is a multimodal request
            string? promptForApi = null;
            if (hasImages)
            {
                // For requests with images, the 'text' is the primary prompt.
                // If 'text' is empty, we use the template as a default instruction (e.g., "Describe the image").
                promptForApi = !string.IsNullOrWhiteSpace(text)
                    ? text
                    : promptTemplate.Replace("{message}", "").Trim();
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                // For text-only requests, use the template to enhance the message.
                promptForApi = promptTemplate.Replace("{message}", text);
            }

            // Add the text part if we have something to say
            if (!string.IsNullOrWhiteSpace(promptForApi))
            {
                parts.Add(new Part(Text: promptForApi, InlineData: null));
            }

            // Add all image parts
            if (hasImages)
            {
                parts.AddRange(images.Select(img => new Part(Text: null, InlineData: new InlineData("image/jpeg", Convert.ToBase64String(img)))));
            }

            return new GeminiRequest(new List<Content> { new(parts) });
        }
        // ====================================================================================
        // == END OF FIX
        // ====================================================================================


        // Scoped these DTOs to the class as they are only used here.
        private record GeminiRequest(List<Content> contents);
        private record Content(List<Part> parts);
        private record Part([property: JsonPropertyName("text")] string? Text, [property: JsonPropertyName("inline_data")] InlineData? InlineData);
        private record InlineData([property: JsonPropertyName("mime_type")] string MimeType, [property: JsonPropertyName("data")] string Data);
        private record GeminiResponse([property: JsonPropertyName("candidates")] List<Candidate>? Candidates);
        private record Candidate([property: JsonPropertyName("content")] Content? Content);

        #endregion

        #region Pretty Admin Logger

        private class AdminLogger
        {
            private readonly string _operationName;
            private readonly Stopwatch _totalStopwatch;
            private readonly List<LogEntry> _entries = new();
            private string? _finalStatus;

            public string CorrelationId { get; }

            public AdminLogger(string operationName, string correlationId)
            {
                _operationName = operationName;
                CorrelationId = correlationId;
                _totalStopwatch = Stopwatch.StartNew();
                Info($"Operation '{_operationName}' Started");
            }

            public void Info(string message, int? configId = null) => _entries.Add(new LogEntry(message, _totalStopwatch.Elapsed, configId, "INFO"));
            public void Success(string message, int? configId = null)
            {
                _entries.Add(new LogEntry(message, _totalStopwatch.Elapsed, configId, "SUCCESS"));
                _finalStatus = "✅ SUCCESS";
            }
            public void Failure(string message, int? configId = null)
            {
                _entries.Add(new LogEntry(message, _totalStopwatch.Elapsed, configId, "FAILURE"));
                if (_finalStatus == null) _finalStatus = "❌ FAILURE";
            }

            public string Render()
            {
                _totalStopwatch.Stop();
                var sb = new StringBuilder();
                _finalStatus ??= "⚠️ UNKNOWN";

                // Header
                sb.AppendLine("╭─ ✨ Gemini Service Report ✨ ─────────╮");
                sb.AppendLine($"│ Operation:  {_operationName,-25} │");
                sb.AppendLine($"│ Status:     {_finalStatus,-25} │");
                sb.AppendLine($"│ Duration:   {_totalStopwatch.ElapsedMilliseconds,5} ms{new string(' ', 18)} │");
                sb.AppendLine($"│ CID:        {CorrelationId,-25} │");

                // Trace
                sb.AppendLine("├─ 🔎 Trace Log ──────────────────────────┤");
                if (_entries.Any())
                {
                    foreach (var entry in _entries)
                    {
                        string prefix = entry.Status switch { "SUCCESS" => "✅", "FAILURE" => "❌", _ => "➡️" };
                        string configInfo = entry.ConfigId.HasValue ? $"[KeyID:{entry.ConfigId,2}]" : "[System] ";
                        string logLine = $"{prefix} {configInfo} (+{entry.Timestamp.TotalMilliseconds,5:F0}ms) {entry.Message}";

                        sb.Append("│ ").AppendLine(logLine.Truncate(76).PadRight(76)).Append(" │");
                        sb.AppendLine();
                    }
                }
                else
                {
                    sb.AppendLine("│ No trace entries were recorded.        │");
                }

                // Footer
                sb.AppendLine("╰────────────────────────────────────────╯");
                return sb.ToString();
            }

            private record LogEntry(string Message, TimeSpan Timestamp, int? ConfigId, string Status);
        }
        #endregion
    }

    public static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
        }
    }
}