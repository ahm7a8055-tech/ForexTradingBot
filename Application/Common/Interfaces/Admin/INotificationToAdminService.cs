namespace Application.Common.Interfaces
{
    /// <summary>
    /// Defines a service for sending important notifications to the system administrator.
    /// </summary>
    public interface INotificationToAdminService
    {
        /// <summary>
        /// Sends a message to the pre-configured administrator's chat.
        /// </summary>
        /// <param name="message">The text of the message to send. Supports Markdown.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendNotificationAsync(string message, CancellationToken cancellationToken);
    }
}