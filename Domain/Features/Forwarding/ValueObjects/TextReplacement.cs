// File: Domain/Features/Forwarding/ValueObjects/TextReplacement.cs
using System.Text.RegularExpressions;

namespace Domain.Features.Forwarding.ValueObjects
{
    /// <summary>
    /// Represents a single, immutable rule for finding and replacing text within a message.
    /// This is configured as an "Owned Type" in EF Core and will be part of a collection
    /// within MessageEditOptions, typically mapped to its own table.
    /// </summary>
    public class TextReplacement
    {
        #region Properties

        /// <summary>
        /// The text or regular expression pattern to find in the message.
        /// </summary>
        public string Find { get; private set; } = null!;

        /// <summary>
        /// The text that will replace the found pattern. Can be an empty string to
        /// effectively delete the found text.
        /// </summary>
        public string ReplaceWith { get; private set; } = null!;

        /// <summary>
        /// If true, the 'Find' property is treated as a regular expression pattern.
        /// If false, it's treated as a literal text search.
        /// </summary>
        public bool IsRegex { get; private set; }

        /// <summary>
        /// The options to use for regular expression matching (e.g., IgnoreCase, Multiline).
        /// This is only applicable if 'IsRegex' is true.
        /// </summary>
        public RegexOptions RegexOptions { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Private constructor for Entity Framework Core.
        /// Prevents creation of an instance without required parameters from application code.
        /// </summary>
        private TextReplacement() { }

        /// <summary>
        /// Creates a new instance of a text replacement rule.
        /// </summary>
        /// <param name="find">The text or pattern to search for. Cannot be null or empty.</param>
        /// <param name="replaceWith">The text to replace with. Defaults to an empty string if null.</param>
        /// <param name="isRegex">Specifies if the 'find' pattern is a regular expression. Defaults to false.</param>
        /// <param name="regexOptions">Specifies the options for regex matching. Defaults to IgnoreCase.</param>
        /// <exception cref="ArgumentException">Thrown if the 'find' parameter is null or whitespace.</exception>
        public TextReplacement(
            string find,
            string? replaceWith,
            bool isRegex = false,
            RegexOptions regexOptions = RegexOptions.IgnoreCase)
        {
            if (string.IsNullOrWhiteSpace(find))
            {
                throw new ArgumentException("The 'find' parameter cannot be null or empty.", nameof(find));
            }

            Find = find;
            ReplaceWith = replaceWith ?? string.Empty;
            IsRegex = isRegex;
            RegexOptions = regexOptions;
        }

        #endregion
    }
}