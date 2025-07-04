namespace Application.Interfaces
{
    /// <summary>
    /// Defines a service for providing unique, non-repeating advice to users.
    /// </summary>
    public interface IAdviceService
    {
        /// <summary>
        /// Gets the next unique piece of advice from the channel's queue.
        /// Avoids repeating advice until all available advice has been posted.
        /// </summary>
        /// <returns>A unique advice string.</returns>
        string GetNextUniqueAdviceForChannel();


        /// <summary>
        /// Gets a unique piece of advice for a specific user. It avoids repeating advice
        /// until all available advice has been shown to that user.
        /// </summary>
        /// <param name="userId">A unique identifier for the user (e.g., Telegram User ID).</param>
        /// <returns>A unique advice string.</returns>
        string GetUniqueAdviceForUser(long userId);
    }
}