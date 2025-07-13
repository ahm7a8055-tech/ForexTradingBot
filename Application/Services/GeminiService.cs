using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics; // Required for Stopwatch
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Services
{
    /// <summary>
    /// A robust, resilient, and performant service for interacting with Google's Gemini API.
    /// Features include:
    /// - Idempotency: Caches successful responses to prevent duplicate processing and token usage.
    /// - Intelligent Failover: Automatically tries multiple API keys if one fails.
    /// - Preferred Key Logic: Remembers the last successful key and tries it first to minimize latency.
    /// - Rate-Limit Handling: Temporarily "blacklists" keys that hit a rate limit.
    /// - Comprehensive Logging & Admin Notifications: Provides deep insight into the service's state.
    /// </summary>
    public class GeminiService : IGeminiService
    {
        // Dependencies
        private readonly ILogger<GeminiService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMemoryCache _memoryCache;

        // Constants for caching keys
        private const string RateLimitCachePrefix = "GeminiRateLimit_";
        private const string LastSuccessfulConfigCacheKey = "Gemini_LastSuccessfulConfigId";

        // Static JSON options for performance
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public GeminiService(
            ILogger<GeminiService> logger,
            IHttpClientFactory httpClientFactory,
            IServiceProvider serviceProvider,
            IMemoryCache memoryCache)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("GeminiClient");
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            _serviceProvider = serviceProvider;
            _memoryCache = memoryCache;
        }

        // --- PUBLIC METHODS ---

        public async Task<string?> EnhanceMessageAsync(string? text, ICollection<byte[]>? imageDatas, CancellationToken cancellationToken, string? apiKeyName = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var adminReport = new StringBuilder("### 🤖 Gemini Service Report (Multimodal) ###\n");

            try
            {
                // 1. INPUT VALIDATION
                if (string.IsNullOrWhiteSpace(text) && (imageDatas == null || !imageDatas.Any()))
                {
                    _logger.LogWarning("EnhanceMessageAsync called with no content.");
                    return null;
                }
                adminReport.AppendLine($"**Input:** Text `({text?.Length ?? 0} chars)`, Images `({imageDatas?.Count ?? 0})`");

                // 2. IDEMPOTENCY CACHE CHECK
                string cacheKey = GenerateCacheKey(text, imageDatas);
                if (_memoryCache.TryGetValue(cacheKey, out string? cachedResult))
                {
                    stopwatch.Stop();
                    _logger.LogInformation("Returning cached result for message key {CacheKey} in {ElapsedMs}ms.", cacheKey, stopwatch.ElapsedMilliseconds);
                    // No need to notify admin for a simple cache hit, as it's normal operation.
                    return cachedResult;
                }
                adminReport.AppendLine($"**Cache:** `MISS` for key `{cacheKey}`.");

                // --- SLOW PATH: API CALL REQUIRED ---
                await using var scope = _serviceProvider.CreateAsyncScope();
                var scopedServiceProvider = scope.ServiceProvider;
                var notifToAdmin = scopedServiceProvider.GetRequiredService<INotificationToAdminService>();

                var configRepository = scopedServiceProvider.GetRequiredService<IAiApiConfigurationRepository>();

                // 3. GET AND PREPARE API CONFIGURATIONS
                var allConfigs = (await configRepository.GetAllByProviderAndStatusAndKeyNameAsync("Gemini", isEnabled: true, apiKeyName, cancellationToken))
                    .Where(c => c != null && !string.IsNullOrWhiteSpace(c.ApiKey) && !string.IsNullOrWhiteSpace(c.ModelName))
                    .ToList();

                _memoryCache.TryGetValue(LastSuccessfulConfigCacheKey, out int? preferredConfigId);

                var attemptOrder = allConfigs
                    .Where(c => !_memoryCache.TryGetValue($"{RateLimitCachePrefix}{c.Id}", out _))
                    .OrderBy(c => c.Id == preferredConfigId ? 0 : 1)
                    .ThenBy(c => Guid.NewGuid())
                    .ToList();

                adminReport.AppendLine($"**API Keys:** Found `{allConfigs.Count}` total, `{attemptOrder.Count}` usable. Preferred ID: `{preferredConfigId?.ToString() ?? "None"}`.");

                if (!attemptOrder.Any())
                {
                    await notifToAdmin.SendNotificationAsync($"⚠️ **GeminiService Alert**\nNo usable (non-rate-limited) configurations found for ApiKeyName `{apiKeyName ?? "default"}`.", cancellationToken);
                    return null;
                }

                // 4. FAILOVER LOOP
                foreach (var config in attemptOrder)
                {
                    var parts = new List<Part> { new(config.PromptTemplate.Replace("{message}", text ?? string.Empty), null) };
                    if (imageDatas != null) foreach (var imageData in imageDatas) parts.Add(new Part(null, new InlineData("image/jpeg", Convert.ToBase64String(imageData))));
                    var requestBody = new GeminiRequest(new List<Content> { new(parts) });

                    string? enhancedMessage = await AttemptApiCallAsync(config, requestBody, notifToAdmin, cancellationToken);

                    if (!string.IsNullOrWhiteSpace(enhancedMessage))
                    {
                        // 5. SUCCESS!
                        stopwatch.Stop();
                        _memoryCache.Set(cacheKey, enhancedMessage, TimeSpan.FromHours(1));
                        _memoryCache.Set(LastSuccessfulConfigCacheKey, config.Id, TimeSpan.FromDays(7));

                        _logger.LogInformation("Success with ConfigId {ConfigId}. Setting as preferred key. Total time: {ElapsedMs}ms.", config.Id, stopwatch.ElapsedMilliseconds);
                        adminReport.AppendLine($"✅ **Success!**\n**Winning Config ID:** `{config.Id}`\n**Total Time:** `{stopwatch.ElapsedMilliseconds}ms`");
                        await notifToAdmin.SendNotificationAsync(adminReport.ToString(), CancellationToken.None);
                        return enhancedMessage;
                    }
                }

                // 6. TOTAL FAILURE
                stopwatch.Stop();
                adminReport.AppendLine($"❌ **Total Failure!**\nAll `{attemptOrder.Count}` usable configurations failed. Total time: `{stopwatch.ElapsedMilliseconds}ms`");
                await notifToAdmin.SendNotificationAsync(adminReport.ToString(), CancellationToken.None);
                _logger.LogError("All {Count} usable Gemini configurations failed.", attemptOrder.Count);
                return null;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "FATAL unhandled exception in multimodal EnhanceMessageAsync. Total time: {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);
                adminReport.AppendLine($"💥 **FATAL CRASH!**\n**Error:** `{ex.Message}`\n**Time:** `{stopwatch.ElapsedMilliseconds}ms`");
                // Use a different scope for the final notification in case the first one was disposed by the exception.
                await using var finalScope = _serviceProvider.CreateAsyncScope();
                await finalScope.ServiceProvider.GetRequiredService<INotificationToAdminService>().SendNotificationAsync(adminReport.ToString(), CancellationToken.None);
                return null;
            }
        }

        // The text-only version would be a simplified copy of the above, with identical logic.
        public Task<string?> EnhanceMessageAsync(string originalMessage, CancellationToken cancellationToken, string? apiKeyName = null)
        {
            return EnhanceMessageAsync(originalMessage, null, cancellationToken, apiKeyName);
        }

        // --- PRIVATE HELPER METHODS ---

        private async Task<string?> AttemptApiCallAsync(AiApiConfiguration config, GeminiRequest requestBody, INotificationToAdminService notifToAdmin, CancellationToken cancellationToken)
        {
            var uri = $"https://generativelanguage.googleapis.com/v1beta/models/{config.ModelName}:generateContent?key={config.ApiKey}";
            _logger.LogInformation("--> Attempting API call to model: {ModelName} for ConfigId: {ConfigId}", config.ModelName, config.Id);

            try
            {
                var response = await _httpClient.PostAsJsonAsync(uri, requestBody, _jsonSerializerOptions, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
                    string? enhancedText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.parts?.FirstOrDefault()?.Text;
                    if (string.IsNullOrWhiteSpace(enhancedText))
                    {
                        _logger.LogWarning("<-- API call for ConfigId {ConfigId} succeeded but returned empty content.", config.Id);
                        return null;
                    }
                    return enhancedText.Trim();
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("<-- Rate limit hit for ConfigId {ConfigId}. Blacklisting for 60 seconds.", config.Id);
                    _memoryCache.Set($"{RateLimitCachePrefix}{config.Id}", true, TimeSpan.FromSeconds(60));
                }

                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("<-- API call for ConfigId {ConfigId} failed with status {StatusCode}. Response: {ErrorContent}", config.Id, response.StatusCode, errorContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "<-- An unexpected exception occurred during the API call for ConfigId {ConfigId}.", config.Id);
                // The exception will be caught and reported by the main EnhanceMessageAsync method.
                return null;
            }
        }

        private static string GenerateCacheKey(string? text, ICollection<byte[]>? imageDatas)
        {
            using var sha256 = SHA256.Create();
            using var combinedStream = new MemoryStream();

            if (!string.IsNullOrWhiteSpace(text))
            {
                var textBytes = Encoding.UTF8.GetBytes($"TEXT_BLOCK:{text}");
                combinedStream.Write(textBytes, 0, textBytes.Length);
            }
            if (imageDatas != null && imageDatas.Any())
            {
                var sortedImages = imageDatas.Where(d => d != null && d.Length > 0).OrderBy(d => Convert.ToBase64String(d), StringComparer.Ordinal);
                foreach (var imageData in sortedImages)
                {
                    var imageHeader = Encoding.UTF8.GetBytes($"IMAGE_BLOCK_LEN:{imageData.Length}:");
                    combinedStream.Write(imageHeader, 0, imageHeader.Length);
                    combinedStream.Write(imageData, 0, imageData.Length);
                }
            }
            if (combinedStream.Length == 0) return "GeminiResult_EmptyInput";

            combinedStream.Position = 0;
            var hashBytes = sha256.ComputeHash(combinedStream);
            return $"GeminiResult-{Convert.ToBase64String(hashBytes)}";
        }

        // --- DTO Records ---
        public record GeminiRequest(List<Content> contents);
        public record Content(List<Part> parts);
        public record Part([property: JsonPropertyName("text"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text, [property: JsonPropertyName("inline_data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InlineData? InlineData);
        public record InlineData([property: JsonPropertyName("mime_type")] string MimeType, [property: JsonPropertyName("data")] string Data);
        public record GeminiResponse([property: JsonPropertyName("candidates")] List<Candidate>? Candidates);
        public record Candidate([property: JsonPropertyName("content")] Content? Content);
    }
}