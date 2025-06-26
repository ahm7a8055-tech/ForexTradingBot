// File: Domain\Features\Forwarding\ValueObjects\MessageEditOptions.cs
// این فایل نیاز به using برای TextReplacement ندارد زیرا TextReplacement
// در همین namespace Domain.Features.Forwarding.ValueObjects تعریف شده است.
using Microsoft.EntityFrameworkCore;
namespace Domain.Features.Forwarding.ValueObjects
{
    /// <summary>
    /// تنظیمات و گزینه‌های قابل اعمال بر ویرایش پیام‌ها در هنگام فوروارد یا پردازش متن.
    /// این کلاس به عنوان Value Object استفاده می‌شود و تغییرناپذیر (immutable) طراحی شده است.
    /// </summary>
    [Owned]
    public class MessageEditOptions
    {
        #region Properties



        /// <summary>
        /// Custom text to be prepended to the message content.
        /// </summary>
        public string? PrependText { get; private set; }

        /// <summary>
        /// Custom text to be appended to the message content.
        /// </summary>
        public string? AppendText { get; private set; }

        /// <summary>
        /// A list of text replacement rules to be applied to the message.
        /// Configured as a separate table owned by MessageEditOptions.
        /// </summary>
        public IReadOnlyList<TextReplacement> TextReplacements { get; private set; }

        /// <summary>
        /// If true, the "Forwarded from..." header is removed from the message.
        /// </summary>
        public bool RemoveSourceForwardHeader { get; private set; }

        /// <summary>
        /// If true, all URLs are stripped from the message text.
        /// </summary>
        public bool RemoveLinks { get; private set; }

        /// <summary>
        /// If true, all formatting (bold, italic, etc.) is removed, sending a plain text message.
        /// </summary>
        public bool StripFormatting { get; private set; }

        /// <summary>
        /// Custom footer text to add at the very end of the message.
        /// </summary>
        public string? CustomFooter { get; private set; }

        /// <summary>
        /// If true, the original message author's name is dropped.
        /// </summary>
        public bool DropAuthor { get; private set; }

        /// <summary>
        /// If true, captions for media (photos, videos) are removed.
        /// </summary>
        public bool DropMediaCaptions { get; private set; }

        /// <summary>
        /// If true, disables the ability for others to forward the message.
        /// </summary>
        public bool NoForwards { get; private set; }

        #endregion

        #region Constructors and Methods

        /// <summary>
        /// Private constructor for Entity Framework Core.
        /// </summary>
        private MessageEditOptions()
        {
            // Initialize collections to prevent null reference issues.
            TextReplacements = [];
        }

        /// <summary>
        /// Creates a new instance of MessageEditOptions with specified values.
        /// </summary>
        public MessageEditOptions(
            string? prependText,
            string? appendText,
            IReadOnlyList<TextReplacement>? textReplacements,
            bool removeSourceForwardHeader,
            bool removeLinks,
            bool stripFormatting,
            string? customFooter,
            bool dropAuthor,
            bool dropMediaCaptions,
            bool noForwards)
        {
            PrependText = prependText;
            AppendText = appendText;
            TextReplacements = textReplacements ?? [];
            RemoveSourceForwardHeader = removeSourceForwardHeader;
            RemoveLinks = removeLinks;
            StripFormatting = stripFormatting;
            CustomFooter = customFooter;
            DropAuthor = dropAuthor;
            DropMediaCaptions = dropMediaCaptions;
            NoForwards = noForwards;
        }
        #endregion
        /// <summary>
        /// متد ساخت یک نمونه‌ی جدید از <see cref="MessageEditOptions"/> با اعمال تغییرات دلخواه بر فیلدهای خاص،
        /// بدون تغییر سایر مقادیر موجود. این الگو به عنوان "Builder-Like" برای حفظ تغییرناپذیری (immutability) استفاده می‌شود.
        /// </summary>
        /// <param name="prependText">مقدار جدید برای PrependText. اگر null باشد، مقدار فعلی حفظ می‌شود.</param>
        /// <param name="appendText">مقدار جدید برای AppendText. اگر null باشد، مقدار فعلی حفظ می‌شود.</param>
        /// <param name="textReplacements">مقدار جدید برای TextReplacements. اگر null باشد، مقدار فعلی حفظ می‌شود.</param>
        /// <param name="removeSourceForwardHeader">مقدار جدید برای RemoveSourceForwardHeader. اگر null باشد، مقدار فعلی حفظ می‌شود.</param>
        /// <param name="removeLinks">مقدار جدید برای RemoveLinks. اگر null باشد، مقدار فعلی حفظ می‌شود.</param>
        /// <param name="stripFormatting">مقدار جدید برای StripFormatting. اگر null باشد، مقدار فعلی حفظ می‌شود.</param>
        /// <param name="customFooter">مقدار جدید برای CustomFooter. اگر null باشد، مقدار فعلی حفظ می‌شود.</param>
        /// <param name="dropAuthor">مقدار جدید برای DropAuthor. اگر null باشد، مقدار فعلی حفظ می‌شود.</param>
        /// <param name="dropMediaCaptions">مقدار جدید برای DropMediaCaptions. اگر null باشد، مقدار فعلی حفظ می‌شود.</param>
        /// <param name="noForwards">مقدار جدید برای NoForwards. اگر null باشد، مقدار فعلی حفظ می‌شود.</param>
        /// <returns>یک نمونه جدید از <see cref="MessageEditOptions"/> با تغییرات اعمال شده.</returns>
        public MessageEditOptions With(
            string? prependText = null,
            string? appendText = null,
            IReadOnlyList<TextReplacement>? textReplacements = null,
            bool? removeSourceForwardHeader = null,
            bool? removeLinks = null,
            bool? stripFormatting = null,
            string? customFooter = null,
            bool? dropAuthor = null,
            bool? dropMediaCaptions = null,
            bool? noForwards = null)
        {
            return new MessageEditOptions(
                prependText ?? PrependText,
                appendText ?? AppendText,
                textReplacements ?? TextReplacements,
                removeSourceForwardHeader ?? RemoveSourceForwardHeader,
                removeLinks ?? RemoveLinks,
                stripFormatting ?? StripFormatting,
                customFooter ?? CustomFooter,
                dropAuthor ?? DropAuthor,
                dropMediaCaptions ?? DropMediaCaptions,
                noForwards ?? NoForwards
            );
        }
    }
}