namespace Application.Common.Interfaces
{
    /// <summary>
    /// Defines a service for enhancing text messages using an AI provider like Gemini.
    /// </summary>
    public interface IGeminiService
    {
        /// <summary>
        /// Enhances a given message text using the configured AI provider.
        /// </summary>
        /// <param name="originalMessage">The original text to enhance.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>
        /// The enhanced message text, or null if enhancement fails, is disabled,
        /// or an error occurs (like being out of quota).
        /// </returns>
        Task<string?> EnhanceMessageAsync(string originalMessage, CancellationToken cancellationToken);
    }
}