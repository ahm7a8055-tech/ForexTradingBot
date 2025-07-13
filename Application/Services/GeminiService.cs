using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Services
{
    public class GeminiService : IGeminiService
    {
        // Dependencies
        private readonly ILogger<GeminiService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMemoryCache _cache;

        // Locks per input
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        // Free‐Tier quotas
        private const int FREE_RPM = 15;
        private const int FREE_TPM = 250_000;
        private const int FREE_RPD = 1_000;

        // Cache key patterns
        private const string RPM_KEY = "Quota_RPM_{0}";
        private const string TPM_KEY = "Quota_TPM_{0}";
        private const string RPD_KEY = "Quota_RPD_{0}";
        private const string RATE_LIMITED_KEY = "GeminiRateLimit_{0}";
        private const string PREFERRED_KEY = "Gemini_PreferredConfigId";

        // JSON options
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        // Resilience policies
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;
        private readonly AsyncTimeoutPolicy _timeoutPolicy;

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

            // Configure HttpClient timeouts
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Retry: 3 attempts, exponential backoff 200ms‐800ms, on transient failures (5xx, 408)
            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)),
                    onRetry: (resp, ts, attempt, ctx) =>
                        _logger.LogWarning("Retry {Attempt} for {OperationKey} after {Delay}ms due to {Reason}.",
                            attempt, ctx.OperationKey, ts.TotalMilliseconds, resp.Exception?.Message ?? resp.Result.StatusCode.ToString()));

            // Circuit Breaker: break on 2 consecutive failures, duration 30s
            _circuitBreakerPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => !r.IsSuccessStatusCode && r.StatusCode != HttpStatusCode.TooManyRequests)
                .CircuitBreakerAsync(2, TimeSpan.FromSeconds(30),
                    onBreak: (resp, _) => _logger.LogWarning("Circuit broken due to {Reason}.", resp.Exception?.Message ?? resp.Result.StatusCode.ToString()),
                    onReset: () => _logger.LogInformation("Circuit reset."),
                    onHalfOpen: () => _logger.LogInformation("Circuit half-open test."));

            // Timeout: each call must finish within 10s
            _timeoutPolicy = Policy.TimeoutAsync(10, TimeoutStrategy.Pessimistic,
                onTimeoutAsync: (ctx, ts, _) =>
                {
                    _logger.LogError("Timeout after {Timeout}s in {OperationKey}.", ts.TotalSeconds, ctx.OperationKey);
                    return Task.CompletedTask;
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
            // Correlation ID for this operation
            string correlationId = Guid.NewGuid().ToString("N");
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            });

            _logger.LogInformation("Start EnhanceMessageAsync | CID={CID} | TextLen={Len} | ImgCount={Count}",
                correlationId, text?.Length ?? 0, images?.Count ?? 0);

            // 1. Input validation
            if (string.IsNullOrWhiteSpace(text) && (images == null || !images.Any()))
            {
                _logger.LogWarning("Empty input; aborting.");
                return null;
            }

            // 2. Cache key
            string cacheKey = GenerateCacheKey(text, images);
            var sem = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(ct);

            var sw = Stopwatch.StartNew();
            var adminLog = new StringBuilder()
                .AppendLine($"[CID={correlationId}] GeminiService START")
                .AppendLine($"Timestamp: {DateTime.UtcNow:O}")
                .AppendLine($"CacheKey: {cacheKey}");

            try
            {
                // 3. Idempotency cache
                if (_cache.TryGetValue(cacheKey, out string? cached))
                {
                    _logger.LogInformation("Cache hit | CID={CID}", correlationId);
                    adminLog.AppendLine("Cache hit; returning cached result.");
                    await NotifyAdminAsync(adminLog.ToString(), ct, correlationId);
                    return cached;
                }

                adminLog.AppendLine("Cache miss; proceeding to API.");

                // 4. Load configs
                await using var svcScope = _serviceProvider.CreateAsyncScope();
                var repo = svcScope.ServiceProvider.GetRequiredService<IAiApiConfigurationRepository>();
                var adminNotifier = svcScope.ServiceProvider.GetRequiredService<INotificationToAdminService>();

                var configs = (await repo.GetAllByProviderAndStatusAndKeyNameAsync("Gemini", true, apiKeyName, ct))
                    .Where(c => !string.IsNullOrWhiteSpace(c.ApiKey) && !string.IsNullOrWhiteSpace(c.ModelName))
                    .ToList();

                _logger.LogDebug("Configs loaded: {Count}", configs.Count);
                if (!configs.Any())
                {
                    adminLog.AppendLine("No configs found.");
                    await adminNotifier.SendNotificationAsync(adminLog.ToString(), ct);
                    return null;
                }

                // 5. Prioritize
                _cache.TryGetValue(PREFERRED_KEY, out int? preferredId);
                var ordered = PrioritizeConfigs(configs, preferredId);
                adminLog.AppendLine($"PreferredId: {preferredId} | Ordered: {string.Join(",", ordered.Select(c => c.Id))}");

                // 6. Try each config
                foreach (var cfg in ordered)
                {
                    adminLog.AppendLine($"Trying Config {cfg.Id} | Model={cfg.ModelName}");
                    var result = await AttemptCallWithPoliciesAsync(cfg, text, images, ct, adminLog, correlationId);
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        // Success
                        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
                        _cache.Set(PREFERRED_KEY, cfg.Id, TimeSpan.FromDays(7));

                        adminLog.AppendLine($"Success with {cfg.Id} in {sw.ElapsedMilliseconds}ms");
                        await adminNotifier.SendNotificationAsync(adminLog.ToString(), ct);
                        return result;
                    }
                    adminLog.AppendLine($"Config {cfg.Id} failed; next.");
                }

                adminLog.AppendLine($"All configs failed after {sw.ElapsedMilliseconds}ms");
                await adminNotifier.SendNotificationAsync(adminLog.ToString(), ct);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception | CID={CID}", correlationId);
                adminLog.AppendLine($"Exception: {ex}");
                await NotifyAdminAsync(adminLog.ToString(), ct, correlationId);
                return null;
            }
            finally
            {
                sem.Release();
            }
        }

        #region Helpers

        private List<AiApiConfiguration> PrioritizeConfigs(List<AiApiConfiguration> all, int? preferredId)
        {
            var usable = all.Where(c => !_cache.TryGetValue(string.Format(RATE_LIMITED_KEY, c.Id), out _)).ToList();
            if (preferredId.HasValue)
            {
                var pref = usable.FirstOrDefault(x => x.Id == preferredId);
                if (pref != null)
                {
                    usable.Remove(pref);
                    usable.Insert(0, pref);
                }
            }
            var rnd = new Random();
            return usable.Take(1).Concat(usable.Skip(1).OrderBy(_ => rnd.Next())).ToList();
        }

        private async Task<string?> AttemptCallWithPoliciesAsync(
            AiApiConfiguration cfg,
            string? text,
            ICollection<byte[]>? images,
            CancellationToken ct,
            StringBuilder adminLog,
            string correlationId)
        {
            // Quota check
            int tokens = (text?.Length ?? 0) + (images?.Sum(i => i.Length) ?? 0);
            if (!CheckAndIncrementQuota(cfg.ModelName, tokens))
            {
                adminLog.AppendLine($"Quota exceeded for {cfg.ModelName}; skipping.");
                return null;
            }

            // Prepare request
            var parts = new List<Part> { new(cfg.PromptTemplate.Replace("{message}", text ?? ""), null) };
            if (images != null)
                foreach (var img in images)
                    parts.Add(new Part(null, new InlineData("image/jpeg", Convert.ToBase64String(img))));

            var req = new GeminiRequest(new List<Content> { new(parts) });
            var uri = $"https://generativelanguage.googleapis.com/v1beta/models/{cfg.ModelName}:generateContent?key={cfg.ApiKey}";

            Context pollyCtx = new Context($"GeminiCall-{cfg.Id}-{correlationId}");

            try
            {
                // Compose policies: Timeout → CircuitBreaker → Retry → HTTP call
                var response = await _timeoutPolicy
                    .WrapAsync(_circuitBreakerPolicy.WrapAsync(_retryPolicy))
                    .ExecuteAsync(ctx => _httpClient.PostAsJsonAsync(uri, req, _jsonOpts, ct), pollyCtx);

                adminLog.AppendLine($"HTTP {(int)response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    var gem = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
                    var txt = gem?.Candidates?.FirstOrDefault()?.Content?.parts?.FirstOrDefault()?.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(txt))
                    {
                        adminLog.AppendLine("Valid content received.");
                        return txt;
                    }
                    adminLog.AppendLine("Empty content from Gemini.");
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    adminLog.AppendLine("429 TooManyRequests; blacklisting for 60s.");
                    _cache.Set(string.Format(RATE_LIMITED_KEY, cfg.Id), true, TimeSpan.FromSeconds(60));
                    _cache.Remove(PREFERRED_KEY);
                }

                return null;
            }
            catch (BrokenCircuitException)
            {
                adminLog.AppendLine("Circuit is open; skipping key.");
                return null;
            }
            catch (TimeoutRejectedException)
            {
                adminLog.AppendLine("Request timed out by policy.");
                return null;
            }
            catch (Exception ex)
            {
                adminLog.AppendLine($"Network/HTTP exception: {ex.Message}");
                _logger.LogError(ex, "Error in AttemptCallWithPoliciesAsync | CID={CID} | Config={Id}", correlationId, cfg.Id);
                return null;
            }
        }

        private bool CheckAndIncrementQuota(string model, int tokens)
        {
            var rpmKey = string.Format(RPM_KEY, model);
            var tpmKey = string.Format(TPM_KEY, model);
            var rpdKey = string.Format(RPD_KEY, model);

            int currentRpm = _cache.Get<int?>(rpmKey) ?? 0;
            int currentTpm = _cache.Get<int?>(tpmKey) ?? 0;
            int currentRpd = _cache.Get<int?>(rpdKey) ?? 0;

            if (currentRpm + 1 > FREE_RPM) return false;
            if (currentTpm + tokens > FREE_TPM) return false;
            if (currentRpd + 1 > FREE_RPD) return false;

            _cache.Set(rpmKey, currentRpm + 1, TimeSpan.FromMinutes(1));
            _cache.Set(tpmKey, currentTpm + tokens, TimeSpan.FromMinutes(1));
            _cache.Set(rpdKey, currentRpd + 1, DateTime.Today.AddDays(1));

            return true;
        }

        private static string GenerateCacheKey(string? text, ICollection<byte[]>? images)
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
            if (ms.Length == 0) return "EMPTY";
            ms.Position = 0;
            var hash = sha.ComputeHash(ms);
            return Convert.ToBase64String(hash);
        }

        private async Task NotifyAdminAsync(string message, CancellationToken ct, string correlationId)
        {
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var notifier = scope.ServiceProvider.GetRequiredService<INotificationToAdminService>();
                await notifier.SendNotificationAsync($"[CID={correlationId}]\n" + message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify admin | CID={CID}", correlationId);
            }
        }

        #endregion

        #region DTOs

        public record GeminiRequest(List<Content> contents);
        public record Content(List<Part> parts);
        public record Part(
            [property: JsonPropertyName("text")] string? Text,
            [property: JsonPropertyName("inline_data")] InlineData? InlineData);
        public record InlineData(
            [property: JsonPropertyName("mime_type")] string MimeType,
            [property: JsonPropertyName("data")] string Data);
        public record GeminiResponse(
            [property: JsonPropertyName("candidates")] List<Candidate>? Candidates);
        public record Candidate(
            [property: JsonPropertyName("content")] Content? Content);

        #endregion
    }
}
