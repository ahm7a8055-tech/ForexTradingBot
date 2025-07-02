// --- File: Infrastructure/Logging/SinkState.cs ---

using Serilog.Events;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace TelegramPanel.Infrastructure.Logging
{
    /// <summary>
    /// Maintains state for the "God Mode" logging sink, enabling intelligent error deduplication and throttling.
    /// It avoids alert storms by tracking the frequency and recency of unique error events.
    /// </summary>
    public class SinkState
    {
        private readonly ConcurrentDictionary<string, ErrorOccurrence> _errorCache = new();
        private readonly TimeSpan _throttlingPeriod = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Represents the occurrence details of a specific error signature.
        /// </summary>
        private class ErrorOccurrence
        {
            public int Count { get; set; } = 1;
            public DateTime FirstSeen { get; } = DateTime.UtcNow;
            public DateTime LastNotification { get; set; } = DateTime.UtcNow;
        }

        /// <summary>
        /// Determines whether a log event should be throttled based on its error signature.
        /// </summary>
        /// <param name="logEvent">The log event to evaluate.</param>
        /// <param name="occurrenceCount">Outputs the total count of occurrences for this specific signature.</param>
        /// <returns>True if throttling is active and the notification should be suppressed; otherwise, false.</returns>
        public bool ShouldThrottle(LogEvent logEvent, out int occurrenceCount)
        {
            string signature = GenerateErrorSignature(logEvent);
            occurrenceCount = 1;

            if (_errorCache.TryGetValue(signature, out var occurrence))
            {
                occurrence.Count++;
                occurrenceCount = occurrence.Count;

                if (DateTime.UtcNow - occurrence.LastNotification < _throttlingPeriod)
                {
                    // Still within throttling window — suppress notification
                    return true;
                }

                // Throttling expired — allow and update timestamp
                occurrence.LastNotification = DateTime.UtcNow;
                return false;
            }

            // First time seeing this error — store it
            _errorCache.TryAdd(signature, new ErrorOccurrence());
            return false;
        }

        /// <summary>
        /// Generates a stable and unique hash for the log event, based on its exception type, message, and caller.
        /// </summary>
        /// <param name="logEvent">The log event to create a signature for.</param>
        /// <returns>A base64-encoded SHA-256 hash string representing the error identity.</returns>
        private string GenerateErrorSignature(LogEvent logEvent)
        {
            var sb = new StringBuilder();

            if (logEvent.Exception is not null)
                sb.Append(logEvent.Exception.GetType().FullName);

            sb.Append(logEvent.MessageTemplate.Text);

            if (logEvent.Properties.TryGetValue("Caller", out var caller))
                sb.Append(caller.ToString("l", null));

            using var sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] hash = sha.ComputeHash(bytes);

            return Convert.ToBase64String(hash);
        }
    }
}
