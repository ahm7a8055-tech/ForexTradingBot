using StackExchange.Redis;
using Telegram.Bot.Types;

namespace TelegramPanel.Queue.Models
{
    /// <summary>
    /// Wraps a deserialized Telegram Update with its original raw Redis value.
    /// This is essential for the reliable queue pattern to acknowledge (delete) the message
    /// from the processing queue after it has been successfully handled.
    /// </summary>
    public class QueueMessage
    {
        /// <summary>
        /// The deserialized Telegram Update object. Can be null if deserialization fails.
        /// </summary>
        public Update? DeserializedUpdate { get; }

        /// <summary>
        /// The original, raw RedisValue that was popped from the queue.
        /// </summary>
        public RedisValue RawValue { get; }

        public QueueMessage(Update? deserializedUpdate, RedisValue rawValue)
        {
            DeserializedUpdate = deserializedUpdate;
            RawValue = rawValue;
        }
    }
}