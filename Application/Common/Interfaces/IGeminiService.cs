namespace Application.Common.Interfaces
{
    /// <summary>
    /// Defines a service for enhancing content using an AI provider like Gemini.
    /// This service supports both text-only and multimodal (text + image) enhancements.
    /// </summary>
    public interface IGeminiService
    {
        /// <summary>
        /// Enhances a message that may contain both text and images using a compatible vision model.
        /// </summary>
        /// <param name="text">The optional text content to enhance. Can be null if only sending images.</param>
        /// <param name="imageDatas">An optional collection of image data (as byte arrays) to include in the prompt.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="apiKeyName">Optional name of the specific API key configuration to use.</param>
        /// <returns>
        /// The enhanced message text, or null if enhancement fails, is disabled,
        /// or a suitable vision model configuration is not found.
        /// </returns>
        Task<string?> EnhanceMessageAsync(string? text, ICollection<byte[]>? imageDatas, CancellationToken cancellationToken, string? apiKeyName = null);

        /// <summary>
        /// Enhances a given text-only message using the configured AI provider.
        /// </summary>
        /// <param name="originalMessage">The original text to enhance.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="apiKeyName">Optional name of the specific API key configuration to use.</param>
        /// <returns>
        /// The enhanced message text, or null if enhancement fails, is disabled,
        /// or an error occurs.
        /// </returns>
        Task<string?> EnhanceMessageAsync(string originalMessage, CancellationToken cancellationToken, string? apiKeyName = null);
    }
}