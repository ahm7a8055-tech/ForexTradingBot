using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text; // Required for StringBuilder
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly ILogger<GeminiService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider; // We will use this to resolve scoped services
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // CORRECTED CONSTRUCTOR: We DO NOT inject the scoped INotificationToAdminService here anymore.
        public GeminiService(
            ILogger<GeminiService> logger,
            IHttpClientFactory httpClientFactory,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("GeminiClient");
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            _serviceProvider = serviceProvider;
        }

        // --- PUBLIC MULTIMODAL METHOD ---
        public async Task<string?> EnhanceMessageAsync(string? text, ICollection<byte[]>? imageDatas, CancellationToken cancellationToken, string? apiKeyName = null)
        {
            if (string.IsNullOrWhiteSpace(text) && (imageDatas == null || !imageDatas.Any()))
            {
                return null;
            }

            // Create a scope to resolve both the repository and the notification service.
            await using var scope = _serviceProvider.CreateAsyncScope();
            var scopedServiceProvider = scope.ServiceProvider;
            var notifToAdmin = scopedServiceProvider.GetRequiredService<INotificationToAdminService>(); // Get notifier here

            try
            {
                var configRepository = scopedServiceProvider.GetRequiredService<IAiApiConfigurationRepository>();
                var validConfigs = (await configRepository.GetAllByProviderAndStatusAndKeyNameAsync("Gemini", isEnabled: true, apiKeyName, cancellationToken))
                    .Where(c => c != null && !string.IsNullOrWhiteSpace(c.ApiKey) && !string.IsNullOrWhiteSpace(c.ModelName) && c.ModelName.Contains("pro-vision"))
                    .ToList();

                if (!validConfigs.Any())
                {
                    await notifToAdmin.SendNotificationAsync($"⚠️ **GeminiService Alert**\nNo valid 'pro-vision' configurations found for ApiKeyName `{apiKeyName ?? "default"}`.", cancellationToken);
                    return !string.IsNullOrWhiteSpace(text) ? await EnhanceMessageAsync(text, cancellationToken, apiKeyName) : null;
                }

                foreach (var config in validConfigs)
                {
                    // (Logic to build request body remains the same)
                    var parts = new List<Part> { new(config.PromptTemplate.Replace("{message}", text ?? string.Empty), null) };
                    if (imageDatas != null) foreach (var imageData in imageDatas) parts.Add(new Part(null, new InlineData("image/jpeg", Convert.ToBase64String(imageData))));
                    var requestBody = new GeminiRequest(new List<Content> { new(parts) });

                    // Pass the resolved notifier to the API call method
                    string? enhancedMessage = await AttemptApiCallAsync(config, requestBody, notifToAdmin, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(enhancedMessage))
                    {
                        return enhancedMessage;
                    }
                }

                await notifToAdmin.SendNotificationAsync($"🚨 **GeminiService Failure**\nAll {validConfigs.Count} 'pro-vision' configurations failed for ApiKeyName `{apiKeyName ?? "default"}`.", cancellationToken);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in multimodal EnhanceMessageAsync for ApiKeyName: {ApiKeyName}.", apiKeyName);
                await notifToAdmin.SendNotificationAsync($"💥 **GeminiService CRASH**\nMultimodal `EnhanceMessageAsync` failed for `{apiKeyName ?? "default"}`.\n**Error:** `{ex.Message}`", cancellationToken);
                return null;
            }
        }

        // --- TEXT-ONLY METHOD ---
        public async Task<string?> EnhanceMessageAsync(string originalMessage, CancellationToken cancellationToken, string? apiKeyName = null)
        {
            if (string.IsNullOrWhiteSpace(originalMessage))
            {
                return null;
            }

            // Create a scope to resolve both the repository and the notification service.
            await using var scope = _serviceProvider.CreateAsyncScope();
            var scopedServiceProvider = scope.ServiceProvider;
            var notifToAdmin = scopedServiceProvider.GetRequiredService<INotificationToAdminService>(); // Get notifier here

            try
            {
                var configRepository = scopedServiceProvider.GetRequiredService<IAiApiConfigurationRepository>();
                var validConfigs = (await configRepository.GetAllByProviderAndStatusAndKeyNameAsync("Gemini", isEnabled: true, apiKeyName, cancellationToken))
                    .Where(c => c != null && !string.IsNullOrWhiteSpace(c.ApiKey) && !string.IsNullOrWhiteSpace(c.ModelName) && !string.IsNullOrWhiteSpace(c.PromptTemplate))
                    .ToList();

                if (!validConfigs.Any())
                {
                    await notifToAdmin.SendNotificationAsync($"⚠️ **GeminiService Alert**\nNo valid text-only configurations found for ApiKeyName `{apiKeyName ?? "default"}`.", cancellationToken);
                    return null;
                }

                foreach (var config in validConfigs)
                {
                    var requestBody = new GeminiRequest(new List<Content> { new(new List<Part> { new(config.PromptTemplate.Replace("{message}", originalMessage), null) }) });
                    // Pass the resolved notifier to the API call method
                    string? enhancedMessage = await AttemptApiCallAsync(config, requestBody, notifToAdmin, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(enhancedMessage))
                    {
                        return enhancedMessage;
                    }
                }

                await notifToAdmin.SendNotificationAsync($"🚨 **GeminiService Failure**\nAll {validConfigs.Count} text-only configurations failed for ApiKeyName `{apiKeyName ?? "default"}`.", cancellationToken);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in text-only EnhanceMessageAsync for ApiKeyName: {ApiKeyName}.", apiKeyName);
                await notifToAdmin.SendNotificationAsync($"💥 **GeminiService CRASH**\nText-only `EnhanceMessageAsync` failed for `{apiKeyName ?? "default"}`.\n**Error:** `{ex.Message}`", cancellationToken);
                return null;
            }
        }

        // --- CENTRALIZED API CALL LOGIC (MODIFIED TO ACCEPT NOTIFIER) ---
        private async Task<string?> AttemptApiCallAsync(AiApiConfiguration config, GeminiRequest requestBody, INotificationToAdminService notifToAdmin, CancellationToken cancellationToken)
        {
            var uri = $"https://generativelanguage.googleapis.com/v1beta/models/{config.ModelName}:generateContent?key={config.ApiKey}";
            // This detailed log is now removed to avoid leaking the whole prompt/image data in production logs.
            // A shorter log can be used if needed. _logger.LogInformation("Attempting API call with ConfigId: {ConfigId}", config.Id);

            try
            {
                var response = await _httpClient.PostAsJsonAsync(uri, requestBody, _jsonSerializerOptions, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
                    string? enhancedText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.parts?.FirstOrDefault()?.Text;
                    if (string.IsNullOrWhiteSpace(enhancedText))
                    {
                        await notifToAdmin.SendNotificationAsync($"🤔 **GeminiService Warning**\nAPI call succeeded for ConfigId `{config.Id}` but returned **empty content**. Prompt may need review.", cancellationToken);
                        return null;
                    }
                    return enhancedText.Trim();
                }

                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("API call for ConfigId {ConfigId} failed with status {StatusCode}. Response: {ErrorContent}", config.Id, response.StatusCode, errorContent);
                await notifToAdmin.SendNotificationAsync(
                    new StringBuilder()
                        .AppendLine($"🔥 **Gemini API Error**")
                        .AppendLine($"**Config ID:** `{config.Id}` | **Key Name:** `{config.ApiKeyName}`")
                        .AppendLine($"**Status:** `{(int)response.StatusCode} {response.ReasonPhrase}`")
                        .AppendLine($"**Response:** ```{errorContent.Trim()}```")
                        .ToString(),
                    cancellationToken);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected exception occurred during the API call for ConfigId {ConfigId}.", config.Id);
                await notifToAdmin.SendNotificationAsync($"💥 **Gemini API CRASH**\nAPI call failed for ConfigId `{config.Id}`.\n**Error:** `{ex.Message}`", cancellationToken);
                return null;
            }
        }

        // DTO Records...
        public record GeminiRequest(List<Content> contents);
        public record Content(List<Part> parts);
        public record Part([property: JsonPropertyName("text"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text, [property: JsonPropertyName("inline_data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InlineData? InlineData);
        public record InlineData([property: JsonPropertyName("mime_type")] string MimeType, [property: JsonPropertyName("data")] string Data);
        public record GeminiResponse([property: JsonPropertyName("candidates")] List<Candidate>? Candidates);
        public record Candidate([property: JsonPropertyName("content")] Content? Content);
    }
}