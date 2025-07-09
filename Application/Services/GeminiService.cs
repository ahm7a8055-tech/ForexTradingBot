using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Application.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly ILogger<GeminiService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IAiApiConfigurationRepository _configRepository; // Our new repository
        private const string ProviderName = "Gemini";

        public GeminiService(
            ILogger<GeminiService> logger,
            IHttpClientFactory httpClientFactory,
            IAiApiConfigurationRepository configRepository) // Inject the repository
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("GeminiClient");
            _configRepository = configRepository;
        }

        public async Task<string?> EnhanceMessageAsync(string originalMessage, CancellationToken cancellationToken)
        {
            // 1. Get configuration from the database repository.
            // This is fast because the repository uses the high-performance index.
            var config = await _configRepository.GetByProviderAndStatusAsync(ProviderName, isEnabled: true, cancellationToken);

            // 2. Validate the configuration from the database.
            if (config is null)
            {
                _logger.LogTrace("Gemini provider configuration not found or is disabled in the database. Skipping enhancement.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.ModelName) || string.IsNullOrWhiteSpace(config.PromptTemplate))
            {
                _logger.LogWarning("Gemini configuration for provider '{ProviderName}' is incomplete (missing ApiKey, ModelName, or PromptTemplate).", ProviderName);
                return null;
            }

            if (string.IsNullOrWhiteSpace(originalMessage))
            {
                _logger.LogDebug("Original message is empty. Nothing to enhance.");
                return null;
            }

            // 3. The rest of the logic is the same, but uses properties from the 'config' object.
            try
            {
                string prompt = config.PromptTemplate.Replace("{message}", originalMessage);
                var requestBody = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
                string uri = $"https://generativelanguage.googleapis.com/v1beta/models/{config.ModelName}:generateContent?key={config.ApiKey}";

                var response = await _httpClient.PostAsJsonAsync(uri, requestBody, cancellationToken);

                // This handles all errors, including "out of quota" (HTTP 429).
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Gemini API call failed for model '{ModelName}' with status {StatusCode}. The original message will be used. Response: {ErrorContent}",
                        config.ModelName, response.StatusCode, errorContent);
                    return null; // Return null to fall back to the original message.
                }

                var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
                string? enhancedText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

                if (string.IsNullOrWhiteSpace(enhancedText))
                {
                    _logger.LogWarning("Gemini API responded successfully but returned no content for model {ModelName}.", config.ModelName);
                    return null;
                }

                _logger.LogInformation("Successfully enhanced message using Gemini model {ModelName}.", config.ModelName);
                return enhancedText.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected exception occurred while enhancing message with Gemini. The original message will be used.");
                return null; // Ensure we fall back to original message on any exception.
            }
        }
    }

    // Helper classes for deserializing the Gemini API JSON response
    internal record GeminiResponse([property: JsonPropertyName("candidates")] List<Candidate>? Candidates);
    internal record Candidate([property: JsonPropertyName("content")] Content? Content);
    internal record Content([property: JsonPropertyName("parts")] List<Part>? Parts);
    internal record Part([property: JsonPropertyName("text")] string? Text);
}