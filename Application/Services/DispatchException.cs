// File: Application/Services/NotificationDispatchService.cs


namespace Application.Services
{
    [Serializable]
    internal class DispatchException : Exception
    {
        public DispatchException()
        {
        }

        public DispatchException(string? message) : base(message)
        {
        }

        public DispatchException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}