using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection; // Required for CreateAsyncScope
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
        private readonly IServiceProvider _serviceProvider; // Correctly injected
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private const string ProviderName = "Gemini";

        // Custom Exception for failover signaling remains the same
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

        /// <summary>
        /// Enhances a message using Gemini AI. Optionally, a specific ApiKeyName can be provided to filter which Gemini config(s) to use.
        /// If ApiKeyName is null, all enabled configs are considered (with failover). If provided, only configs with that ApiKeyName are used.
        /// </summary>
        public async Task<string?> EnhanceMessageAsync(string originalMessage, CancellationToken cancellationToken, string? apiKeyName = null)
        {
            if (string.IsNullOrWhiteSpace(originalMessage))
            {
                _logger.LogDebug("Original message is empty. Nothing to enhance.");
                return null;
            }

            // --- UPGRADE: Correctly resolve the scoped repository ---
            await using var scope = _serviceProvider.CreateAsyncScope();
            var configRepository = scope.ServiceProvider.GetRequiredService<IAiApiConfigurationRepository>();

            // Use the new method to filter by ApiKeyName if provided
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

        // Explicit interface implementation for IGeminiService
        public async Task<string?> EnhanceMessageAsync(string originalMessage, CancellationToken cancellationToken)
        {
            return await EnhanceMessageAsync(originalMessage, cancellationToken, null);
        }
        private AsyncPolicyWrap<string?> CreateFallbackPolicyChain(List<AiApiConfiguration> configs, string message)
        {
            // 1. Define the ultimate fallback. This is the last line of defense.
            AsyncFallbackPolicy<string?> finalFallback = Policy<string?>
                .Handle<GeminiApiFailoverException>()
                .FallbackAsync(
                    fallbackValue: null,
                    onFallbackAsync: args =>
                    {
                        _logger.LogError(args.Exception, "All available Gemini API configurations have failed.");
                        return Task.CompletedTask;
                    });

            // 2. If there are NO configs, we cannot build a chain.
            // The policy to execute is simply the final fallback itself.
            if (!configs.Any())
            {
                _logger.LogWarning("No Gemini configurations to build a policy chain. Only the final null-returning fallback is active.");
                // We still need to return a "wrap" to match the method signature.
                // The correct way to do this for a single policy is to wrap it with a no-op policy.
                return Policy.WrapAsync(finalFallback, Policy.NoOpAsync<string?>());
            }

            // 3. For one or more configs, build the chain iteratively.
            // Start with the final fallback as the innermost policy.
            AsyncPolicyWrap<string?> policyChain = Policy.WrapAsync(finalFallback);

            // Iterate in reverse so the first config in the list becomes the outermost policy.
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
                // Wrap the new fallback around the existing chain.
                policyChain = fallbackForConfig.WrapAsync(policyChain);
            }

            return policyChain;
        }

        private async Task<string?> AttemptApiCallAsync(AiApiConfiguration config, string message, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Attempting to enhance message using Gemini config Id: {ConfigId}, Model: {ModelName}, ApiKeyName: {ApiKeyName}", config.Id, config.ModelName, config.ApiKeyName);

            var prompt = config.PromptTemplate.Replace("{message}", message);
            var requestBody = new GeminiRequest(new List<ContentPart> { new ContentPart(new List<TextPart> { new TextPart(prompt) }) });
            var uri = $"https://generativelanguage.googleapis.com/v1beta/models/{config.ModelName}:generateContent?key={config.ApiKey}";

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync(uri, requestBody, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new GeminiApiFailoverException($"Network error for config Id: {config.Id}, ApiKeyName: {config.ApiKeyName}", ex);
            }
            catch (OperationCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new GeminiApiFailoverException($"HttpClient timeout for config Id: {config.Id}, ApiKeyName: {config.ApiKeyName}", ex);
            }

            if (response.IsSuccessStatusCode)
            {
                var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
                string? enhancedText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

                if (string.IsNullOrWhiteSpace(enhancedText))
                {
                    throw new GeminiApiFailoverException($"API for config Id {config.Id}, ApiKeyName: {config.ApiKeyName} succeeded but returned empty content.");
                }
                return enhancedText.Trim();
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new GeminiApiFailoverException($"Rate limit (429) for config Id: {config.Id}, ApiKeyName: {config.ApiKeyName}.");
            }

            var geminiError = TryParseGeminiError(errorContent);
            if (geminiError is not null)
            {
                _logger.LogError("Unrecoverable Gemini API error for config Id: {ConfigId}, ApiKeyName: {ApiKeyName}. Status: {StatusCode}. Error Code: {ErrorCode}, Message: {ErrorMessage}",
                    config.Id, config.ApiKeyName, response.StatusCode, geminiError.Error.Code, geminiError.Error.Message);
            }
            else
            {
                _logger.LogError("Unrecoverable Gemini API error for config Id: {ConfigId}, ApiKeyName: {ApiKeyName}. Status: {StatusCode}. Response: {ErrorContent}",
                    config.Id, config.ApiKeyName, response.StatusCode, errorContent);
            }

            response.EnsureSuccessStatusCode();
            return null;
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