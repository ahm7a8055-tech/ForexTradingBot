using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Fallback;
using Polly.Timeout;
using Polly.Wrap;
using System.Net;
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

        public class GeminiApiFailoverException : Exception
        {
            public GeminiApiFailoverException(string message) : base(message) { }
            public GeminiApiFailoverException(string message, Exception innerException) : base(message, innerException) { }
        }

        public GeminiService(
            ILogger<GeminiService> logger,
            IHttpClientFactory httpClientFactory,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("GeminiClient");
            _serviceProvider = serviceProvider;
        }

        public async Task<string?> EnhanceMessageAsync(string originalMessage, CancellationToken cancellationToken, string? apiKeyName = null)
        {
            if (string.IsNullOrWhiteSpace(originalMessage))
            {
                _logger.LogDebug("Original message is empty. Nothing to enhance.");
                return null;
            }

            await using var scope = _serviceProvider.CreateAsyncScope();
            var configRepository = scope.ServiceProvider.GetRequiredService<IAiApiConfigurationRepository>();

            var configs = (await configRepository.GetAllByProviderAndStatusAndKeyNameAsync(ProviderName, isEnabled: true, apiKeyName, cancellationToken))
                .Where(c => !string.IsNullOrWhiteSpace(c.ApiKey) && !string.IsNullOrWhiteSpace(c.ModelName) && !string.IsNullOrWhiteSpace(c.PromptTemplate))
                .ToList();

            if (!configs.Any())
            {
                _logger.LogWarning("No valid Gemini provider configurations found. Skipping enhancement. ApiKeyName: {ApiKeyName}", apiKeyName);
                return null;
            }

            var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(30), TimeoutStrategy.Pessimistic);
            var fallbackPolicyChain = CreateFallbackPolicyChain(configs, originalMessage);
            var resilientPolicy = timeoutPolicy.WrapAsync(fallbackPolicyChain);

            try
            {
                string? enhancedMessage = await resilientPolicy.ExecuteAsync(
                    _ => throw new GeminiApiFailoverException("Starting the API key fallback chain."),
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(enhancedMessage))
                {
                    _logger.LogWarning("All Gemini API configurations were tried, but none returned a valid message. ApiKeyName: {ApiKeyName}", apiKeyName);
                    return null;
                }

                _logger.LogInformation("Successfully enhanced message using Gemini. ApiKeyName: {ApiKeyName}", apiKeyName);
                return enhancedMessage;
            }
            catch (TimeoutRejectedException ex)
            {
                _logger.LogError(ex, "Gemini API call timed out after 30 seconds. The operation was cancelled. ApiKeyName: {ApiKeyName}", apiKeyName);
                return null;
            }
            catch (Exception ex) when (ex is not GeminiApiFailoverException)
            {
                _logger.LogError(ex, "A non-recoverable error occurred during the Gemini API call after exhausting all fallback options. ApiKeyName: {ApiKeyName}", apiKeyName);
                return null;
            }
        }

        public async Task<string?> EnhanceMessageAsync(string originalMessage, CancellationToken cancellationToken)
        {
            return await EnhanceMessageAsync(originalMessage, cancellationToken, null);
        }

        private AsyncPolicyWrap<string?> CreateFallbackPolicyChain(List<AiApiConfiguration> configs, string message)
        {
            AsyncFallbackPolicy<string?> finalFallback = Policy<string?>
                .Handle<GeminiApiFailoverException>()
                .FallbackAsync(
                    fallbackValue: null,
                    onFallbackAsync: args =>
                    {
                        _logger.LogError(args.Exception, "All available Gemini API configurations have failed.");
                        return Task.CompletedTask;
                    });

            if (!configs.Any())
            {
                _logger.LogWarning("No Gemini configurations to build a policy chain. Only the final null-returning fallback is active.");
                return Policy.WrapAsync(finalFallback, Policy.NoOpAsync<string?>());
            }

            IAsyncPolicy<string?> policyChain = finalFallback;

            foreach (var config in configs.AsEnumerable().Reverse())
            {
                var fallbackForConfig = Policy<string?>
                    .Handle<GeminiApiFailoverException>()
                    .FallbackAsync(
                        ct => AttemptApiCallAsync(config, message, ct),
                        onFallbackAsync: args =>
                        {
                            _logger.LogWarning(args.Exception, "Fallback triggered for config {ConfigId}. Trying next configuration.", config.Id);
                            return Task.CompletedTask;
                        }
                    );
                policyChain = fallbackForConfig.WrapAsync(policyChain);
            }

            return (AsyncPolicyWrap<string?>)policyChain;
        }

        private async Task<string?> AttemptApiCallAsync(AiApiConfiguration config, string message, CancellationToken cancellationToken)
        {
            var prompt = config.PromptTemplate.Replace("{message}", message);
            var requestBody = new GeminiRequest(new List<ContentPart> { new ContentPart(new List<TextPart> { new TextPart(prompt) }) });
            var uri = $"https://generativelanguage.googleapis.com/v1beta/models/{config.ModelName}:generateContent?key={config.ApiKey}";

            var safeApiKey = string.IsNullOrEmpty(config.ApiKey) ? "NULL_OR_EMPTY" : $"...{config.ApiKey.Substring(Math.Max(0, config.ApiKey.Length - 4))}";
            var safeUri = $"https://generativelanguage.googleapis.com/v1beta/models/{config.ModelName}:generateContent?key={safeApiKey}";
            var requestBodyJson = JsonSerializer.Serialize(requestBody, _jsonSerializerOptions);
            _logger.LogInformation(
                "Attempting Gemini API call. ConfigId: {ConfigId}, ApiKeyName: {ApiKeyName}, Uri: {SafeUri}, Body: {RequestBody}",
                config.Id, config.ApiKeyName, safeUri, requestBodyJson);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync(uri, requestBody, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
            {
                throw new GeminiApiFailoverException($"HTTP request failed for config Id: {config.Id}, ApiKeyName: {config.ApiKeyName}", ex);
            }

            if (response.IsSuccessStatusCode)
            {
                var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
                string? enhancedText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

                if (string.IsNullOrWhiteSpace(enhancedText))
                {
                    _logger.LogWarning("API for config Id {ConfigId}, ApiKeyName: {ApiKeyName} succeeded but returned empty content.", config.Id, config.ApiKeyName);
                    throw new GeminiApiFailoverException($"API for config Id {config.Id} succeeded but returned empty content.");
                }

                _logger.LogInformation("Gemini API call successful for config Id: {ConfigId}", config.Id);
                return enhancedText.Trim();
            }

            // --- FIX START: Re-integrated structured error parsing for better logging ---
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var geminiError = TryParseGeminiError(errorContent);

            if (geminiError is not null)
            {
                _logger.LogWarning(
                    "Gemini API call failed with a structured error. ConfigId: {ConfigId}, ApiKeyName: {ApiKeyName}, Status: {StatusCode}, ErrorCode: {ErrorCode}, Message: {ErrorMessage}",
                    config.Id,
                    config.ApiKeyName,
                    response.StatusCode,
                    geminiError.Error.Code,
                    geminiError.Error.Message);
            }
            else
            {
                _logger.LogWarning(
                    "Gemini API call failed with a non-structured error. ConfigId: {ConfigId}, ApiKeyName: {ApiKeyName}, Status: {StatusCode}, Response: {ErrorContent}",
                    config.Id,
                    config.ApiKeyName,
                    response.StatusCode,
                    errorContent);
            }

            throw new GeminiApiFailoverException($"API call for config {config.Id} failed with status {response.StatusCode}.");
            // --- FIX END ---
        }

        private static GeminiErrorResponse? TryParseGeminiError(string content)
        {
            try { return JsonSerializer.Deserialize<GeminiErrorResponse>(content, _jsonOptions); }
            catch { return null; }
        }
    }

    // Strongly-typed models for Gemini API
    public record GeminiRequest(List<ContentPart> contents);
    public record ContentPart(List<TextPart> parts);
    public record TextPart(string text);

    public record GeminiResponse([property: JsonPropertyName("candidates")] List<Candidate>? Candidates);
    public record Candidate([property: JsonPropertyName("content")] Content? Content);
    public record Content([property: JsonPropertyName("parts")] List<Part>? Parts);
    public record Part([property: JsonPropertyName("text")] string? Text);

    public record GeminiErrorResponse([property: JsonPropertyName("error")] GeminiError Error);
    public record GeminiError([property: JsonPropertyName("code")] string Code, [property: JsonPropertyName("message")] string Message);
}