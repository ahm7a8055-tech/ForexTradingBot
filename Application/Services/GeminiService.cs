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
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = false };
        private const string ProviderName = "Gemini";

        public GeminiService(
            ILogger<GeminiService> logger,
            IHttpClientFactory httpClientFactory,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            // Set a default timeout on the HttpClient itself
            _httpClient = httpClientFactory.CreateClient("GeminiClient");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Enhances a message using Gemini AI by trying each valid configuration sequentially.
        /// </summary>
        public async Task<string?> EnhanceMessageAsync(string originalMessage, CancellationToken cancellationToken, string? apiKeyName = null)
        {
            if (string.IsNullOrWhiteSpace(originalMessage))
            {
                _logger.LogDebug("Original message is empty. Nothing to enhance.");
                return null;
            }

            await using var scope = _serviceProvider.CreateAsyncScope();
            var configRepository = scope.ServiceProvider.GetRequiredService<IAiApiConfigurationRepository>();

            var validConfigs = (await configRepository.GetAllByProviderAndStatusAndKeyNameAsync(ProviderName, isEnabled: true, apiKeyName, cancellationToken))
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.ApiKey) && !string.IsNullOrWhiteSpace(c.ModelName) && !string.IsNullOrWhiteSpace(c.PromptTemplate))
                .ToList();

            if (!validConfigs.Any())
            {
                _logger.LogWarning("No valid Gemini provider configurations found. Skipping enhancement. ApiKeyName: {ApiKeyName}", apiKeyName);
                return null;
            }

            _logger.LogInformation("Found {Count} valid Gemini configurations to try.", validConfigs.Count);

            // --- Iterate through each configuration until one succeeds ---
            foreach (var config in validConfigs)
            {
                string? enhancedMessage = await AttemptApiCallAsync(config, originalMessage, cancellationToken);

                if (!string.IsNullOrWhiteSpace(enhancedMessage))
                {
                    _logger.LogInformation("Successfully enhanced message using config Id: {ConfigId}, ApiKeyName: {ApiKeyName}", config.Id, config.ApiKeyName);
                    return enhancedMessage; // Success, exit immediately.
                }
                // If null, the loop will continue to the next configuration.
            }

            _logger.LogWarning("All {Count} Gemini API configurations were tried, but none succeeded.", validConfigs.Count);
            return null; // All configurations failed.
        }

        // Explicit interface implementation
        public async Task<string?> EnhanceMessageAsync(string originalMessage, CancellationToken cancellationToken)
        {
            return await EnhanceMessageAsync(originalMessage, cancellationToken, null);
        }

        /// <summary>
        /// Attempts a single API call with a given configuration.
        /// Returns the enhanced message on success, or null on any failure.
        /// </summary>
        private async Task<string?> AttemptApiCallAsync(AiApiConfiguration config, string message, CancellationToken cancellationToken)
        {
            var requestBody = new GeminiRequest(new List<ContentPart> { new ContentPart(new List<TextPart> { new TextPart(config.PromptTemplate.Replace("{message}", message)) }) });
            var uri = $"https://generativelanguage.googleapis.com/v1beta/models/{config.ModelName}:generateContent?key={config.ApiKey}";

            // Log the attempt
            var requestBodyJson = JsonSerializer.Serialize(requestBody, _jsonSerializerOptions);
            _logger.LogInformation("Attempting API call with ConfigId: {ConfigId}, ApiKeyName: {ApiKeyName}, Body: {RequestBody}", config.Id, config.ApiKeyName, requestBodyJson);

            try
            {
                var response = await _httpClient.PostAsJsonAsync(uri, requestBody, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
                    string? enhancedText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

                    if (string.IsNullOrWhiteSpace(enhancedText))
                    {
                        _logger.LogWarning("API call for ConfigId {ConfigId} succeeded but returned empty content.", config.Id);
                        return null; // Failure case
                    }

                    return enhancedText.Trim(); // Success case
                }

                // Handle non-successful status codes
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "API call for ConfigId {ConfigId} failed with status {StatusCode}. Response: {ErrorContent}",
                    config.Id, response.StatusCode, errorContent);
                return null; // Failure case
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex, "API call for ConfigId {ConfigId} timed out.", config.Id);
                return null; // Failure case
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected exception occurred during the API call for ConfigId {ConfigId}.", config.Id);
                return null; // Failure case
            }
        }
    }

    // Strongly-typed models for Gemini API (remain the same)
    public record GeminiRequest(List<ContentPart> contents);
    public record ContentPart(List<TextPart> parts);
    public record TextPart(string text);

    public record GeminiResponse([property: JsonPropertyName("candidates")] List<Candidate>? Candidates);
    public record Candidate([property: JsonPropertyName("content")] Content? Content);
    public record Content([property: JsonPropertyName("parts")] List<Part>? Parts);
    public record Part([property: JsonPropertyName("text")] string? Text);
}