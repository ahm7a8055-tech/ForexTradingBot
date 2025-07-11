using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly ILogger<GeminiService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            WriteIndented = false,
            // This is crucial for multimodal requests to work
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public GeminiService(
            ILogger<GeminiService> logger,
            IHttpClientFactory httpClientFactory,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("GeminiClient");
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // Increased timeout for potential image uploads
            _serviceProvider = serviceProvider;
        }

        // --- NEW PUBLIC MULTIMODAL METHOD ---
        /// <summary>
        /// Enhances a message that can include both text and images using Gemini AI.
        /// </summary>
        public async Task<string?> EnhanceMessageAsync(string? text, ICollection<byte[]>? imageDatas, CancellationToken cancellationToken, string? apiKeyName = null)
        {
            if (string.IsNullOrWhiteSpace(text) && (imageDatas == null || !imageDatas.Any()))
            {
                _logger.LogDebug("Both original message and image data are empty. Nothing to enhance.");
                return null;
            }

            await using var scope = _serviceProvider.CreateAsyncScope();
            var configRepository = scope.ServiceProvider.GetRequiredService<IAiApiConfigurationRepository>();

            var validConfigs = (await configRepository.GetAllByProviderAndStatusAndKeyNameAsync("Gemini", isEnabled: true, apiKeyName, cancellationToken))
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.ApiKey) && !string.IsNullOrWhiteSpace(c.ModelName) && !string.IsNullOrWhiteSpace(c.PromptTemplate) && c.ModelName.Contains("pro-vision")) // Ensure a vision model is used
                .ToList();

            if (!validConfigs.Any())
            {
                _logger.LogWarning("No valid Gemini 'pro-vision' configurations found for multimodal enhancement. ApiKeyName: {ApiKeyName}", apiKeyName);
                // Fallback to text-only if there's text
                return !string.IsNullOrWhiteSpace(text) ? await EnhanceMessageAsync(text, cancellationToken, apiKeyName) : null;
            }

            _logger.LogInformation("Found {Count} valid Gemini vision configurations to try for multimodal enhancement.", validConfigs.Count);

            foreach (var config in validConfigs)
            {
                // Construct the dynamic request body
                var parts = new List<Part>();

                // Add the prompt and text part first
                string fullPrompt = config.PromptTemplate.Replace("{message}", text ?? string.Empty);
                parts.Add(new Part(fullPrompt, null));

                // Add image parts if they exist
                if (imageDatas != null)
                {
                    foreach (var imageData in imageDatas)
                    {
                        // Gemini supports common image formats. JPEG is a safe default.
                        var inlineData = new InlineData("image/jpeg", Convert.ToBase64String(imageData));
                        parts.Add(new Part(null, inlineData));
                        _logger.LogDebug("Added image data (size: {Size} bytes) to the request for ConfigId {ConfigId}.", imageData.Length, config.Id);
                    }
                }

                var requestBody = new GeminiRequest(new List<Content> { new Content(parts) });
                string? enhancedMessage = await AttemptApiCallAsync(config, requestBody, cancellationToken);

                if (!string.IsNullOrWhiteSpace(enhancedMessage))
                {
                    _logger.LogInformation("Successfully enhanced multimodal message using config Id: {ConfigId}, ApiKeyName: {ApiKeyName}", config.Id, config.ApiKeyName);
                    return enhancedMessage; // Success
                }
            }

            _logger.LogWarning("All {Count} Gemini vision API configurations were tried for multimodal message, but none succeeded.", validConfigs.Count);
            return null; // All failed
        }

        // --- EXISTING TEXT-ONLY METHOD (NOW CALLS THE NEW MULTIMODAL METHOD) ---
        public async Task<string?> EnhanceMessageAsync(string originalMessage, CancellationToken cancellationToken, string? apiKeyName = null)
        {
            if (string.IsNullOrWhiteSpace(originalMessage))
            {
                _logger.LogDebug("Original message is empty. Nothing to enhance.");
                return null;
            }

            await using var scope = _serviceProvider.CreateAsyncScope();
            var configRepository = scope.ServiceProvider.GetRequiredService<IAiApiConfigurationRepository>();

            var validConfigs = (await configRepository.GetAllByProviderAndStatusAndKeyNameAsync("Gemini", isEnabled: true, apiKeyName, cancellationToken))
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.ApiKey) && !string.IsNullOrWhiteSpace(c.ModelName) && !string.IsNullOrWhiteSpace(c.PromptTemplate))
                .ToList();

            if (!validConfigs.Any())
            {
                _logger.LogWarning("No valid Gemini provider configurations found. Skipping enhancement. ApiKeyName: {ApiKeyName}", apiKeyName);
                return null;
            }

            _logger.LogInformation("Found {Count} valid Gemini configurations to try for text-only enhancement.", validConfigs.Count);

            foreach (var config in validConfigs)
            {
                // Construct text-only request body
                var requestBody = new GeminiRequest(new List<Content> { new Content(new List<Part> { new Part(config.PromptTemplate.Replace("{message}", originalMessage), null) }) });
                string? enhancedMessage = await AttemptApiCallAsync(config, requestBody, cancellationToken);

                if (!string.IsNullOrWhiteSpace(enhancedMessage))
                {
                    _logger.LogInformation("Successfully enhanced text message using config Id: {ConfigId}, ApiKeyName: {ApiKeyName}", config.Id, config.ApiKeyName);
                    return enhancedMessage; // Success
                }
            }

            _logger.LogWarning("All {Count} Gemini API configurations were tried for text message, but none succeeded.", validConfigs.Count);
            return null; // All failed
        }

        // --- CENTRALIZED API CALL LOGIC ---
        private async Task<string?> AttemptApiCallAsync(AiApiConfiguration config, GeminiRequest requestBody, CancellationToken cancellationToken)
        {
            var uri = $"https://generativelanguage.googleapis.com/v1beta/models/{config.ModelName}:generateContent?key={config.ApiKey}";
            var requestBodyJson = JsonSerializer.Serialize(requestBody, _jsonSerializerOptions);
            _logger.LogInformation("Attempting API call with ConfigId: {ConfigId}, Model: {ModelName}, Body: {RequestBody}", config.Id, config.ModelName, requestBodyJson);

            try
            {
                var response = await _httpClient.PostAsJsonAsync(uri, requestBody, _jsonSerializerOptions, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
                    // =================== FIX IS HERE ===================
                    string? enhancedText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.parts?.FirstOrDefault()?.Text;
                    // ===================================================

                    if (string.IsNullOrWhiteSpace(enhancedText))
                    {
                        _logger.LogWarning("API call for ConfigId {ConfigId} succeeded but returned empty content.", config.Id);
                        return null;
                    }
                    return enhancedText.Trim();
                }

                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("API call for ConfigId {ConfigId} failed with status {StatusCode}. Response: {ErrorContent}", config.Id, response.StatusCode, errorContent);
                return null;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex, "API call for ConfigId {ConfigId} timed out.", config.Id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected exception occurred during the API call for ConfigId {ConfigId}.", config.Id);
                return null;
            }
        }
    }

    // --- UPDATED STRONGLY-TYPED MODELS FOR MULTIMODAL API ---
    public record GeminiRequest(List<Content> contents);

    public record Content(List<Part> parts);

    // A part can now contain EITHER text OR image data, but not both.
    public record Part(
        [property: JsonPropertyName("text"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text,
        [property: JsonPropertyName("inline_data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InlineData? InlineData
    );

    // New record to represent the Base64-encoded image data.
    public record InlineData(
        [property: JsonPropertyName("mime_type")] string MimeType,
        [property: JsonPropertyName("data")] string Data
    );

    // Response models remain the same, as we only expect text back.
    public record GeminiResponse([property: JsonPropertyName("candidates")] List<Candidate>? Candidates);
    public record Candidate([property: JsonPropertyName("content")] Content? Content);
    // Note: The response 'Content' and 'Part' records are structurally the same as the request ones.
}