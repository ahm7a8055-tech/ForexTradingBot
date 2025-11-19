// File: Domain/Features/Forwarding/ValueObjects/MessageFilterOptions.cs
using System.Text.RegularExpressions;

namespace Domain.Features.Forwarding.ValueObjects
{
    /// <summary>
    /// A value object that encapsulates a set of criteria for filtering messages.
    /// This is configured as an "Owned Type" in EF Core, with its properties being mapped
    /// as columns into the owner's table (ForwardingRules).
    /// </summary>
    public class MessageFilterOptions
    {
        #region Properties

        /// <summary>
        /// A list of allowed message types (e.g., "text", "photo"). If empty, all types are considered.
        /// Stored as a JSONB array in the database for efficient querying.
        /// </summary>
        public IReadOnlyList<string> AllowedMessageTypes { get; private set; }

        /// <summary>
        /// A list of allowed MIME types for file-based messages (e.g., "image/jpeg", "application/pdf").
        /// Stored as a JSONB array in the database.
        /// </summary>
        public IReadOnlyList<string> AllowedMimeTypes { get; private set; }

        /// <summary>
        /// A text string or regex pattern that must be present in the message content.
        /// </summary>
        public string? ContainsText { get; private set; }

        /// <summary>
        /// If true, 'ContainsText' is treated as a regular expression.
        /// </summary>
        public bool ContainsTextIsRegex { get; private set; }

        /// <summary>
        /// Regex options to use if 'ContainsTextIsRegex' is true.
        /// </summary>
        public RegexOptions ContainsTextRegexOptions { get; private set; }

        /// <summary>
        /// A list of user IDs from whom messages are allowed. If empty, messages from all senders are considered.
        /// Stored as a JSONB array in the database.
        /// </summary>
        public IReadOnlyList<long> AllowedSenderUserIds { get; private set; }

        /// <summary>
        /// A list of user IDs from whom messages are blocked.
        /// Stored as a JSONB array in the database.
        /// </summary>
        public IReadOnlyList<long> BlockedSenderUserIds { get; private set; }

        /// <summary>
        /// If true, any message that has been edited will be ignored.
        /// </summary>
        public bool IgnoreEditedMessages { get; private set; }

        /// <summary>
        /// If true, service messages (e.g., user joined/left) will be ignored.
        /// </summary>
        public bool IgnoreServiceMessages { get; private set; }

        /// <summary>
        /// The minimum allowed length for the message text. Null indicates no minimum.
        /// </summary>
        public int? MinMessageLength { get; private set; }

        /// <summary>
        /// The maximum allowed length for the message text. Null indicates no maximum.
        /// </summary>
        public int? MaxMessageLength { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Private constructor for Entity Framework Core. Initializes collections.
        /// </summary>
        private MessageFilterOptions()
        {
            // Initialize collections to prevent null reference issues.
            AllowedMessageTypes = [];
            AllowedMimeTypes = [];
            AllowedSenderUserIds = [];
            BlockedSenderUserIds = [];
        }

        /// <summary>
        /// Creates a new instance of MessageFilterOptions with specified criteria.
        /// </summary>
        public MessageFilterOptions(
            IReadOnlyList<string>? allowedMessageTypes,
            IReadOnlyList<string>? allowedMimeTypes,
            string? containsText,
            bool containsTextIsRegex,
            RegexOptions containsTextRegexOptions,
            IReadOnlyList<long>? allowedSenderUserIds,
            IReadOnlyList<long>? blockedSenderUserIds,
            bool ignoreEditedMessages,
            bool ignoreServiceMessages,
            int? minMessageLength,
            int? maxMessageLength)
        {
            AllowedMessageTypes = allowedMessageTypes ?? [];
            AllowedMimeTypes = allowedMimeTypes ?? [];
            ContainsText = containsText;
            ContainsTextIsRegex = containsTextIsRegex;
            ContainsTextRegexOptions = containsTextRegexOptions;
            AllowedSenderUserIds = allowedSenderUserIds ?? [];
            BlockedSenderUserIds = blockedSenderUserIds ?? [];
            IgnoreEditedMessages = ignoreEditedMessages;
            IgnoreServiceMessages = ignoreServiceMessages;
            MinMessageLength = minMessageLength;
            MaxMessageLength = maxMessageLength;
        }

        #endregion
    }
}