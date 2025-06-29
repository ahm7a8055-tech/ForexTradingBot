// --- START OF PLATINUM TIER FILE: EducationalContentHandler.cs ---

using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramPanel.Application.DTOs.Ui; // <-- ADD THIS for our new DTO
using TelegramPanel.Application.Interfaces;
using TelegramPanel.Formatters;
using TelegramPanel.Infrastructure.Helper;
using static TelegramPanel.Infrastructure.ActualTelegramMessageActions;

namespace TelegramPanel.Application.CommandHandlers.Features
{
    /// <summary>
    /// A high-performance, dynamic, and secure handler for serving multi-level, multi-language
    /// educational content from the server's file system.
    /// </summary>
    public class EducationalContentHandler : ITelegramCallbackQueryHandler
    {
        private readonly ILogger<EducationalContentHandler> _logger;
        private readonly ITelegramMessageSender _messageSender;
        private readonly ITelegramBotClient _botClient;
        private readonly IMemoryCacheService<CachedMenuDto> _cache; // <-- FIX: Use the new DTO

        #region Configuration
        private const string BaseLearningPath = @"C:\ForexBotContent";
        private const int PageSize = 8;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
        private static readonly string[] SupportedExtensions = { ".mp3", ".wav", ".mp4", ".mov", ".mkv" };

        private const string EduPrefix = "edu_";
        private const string NavPrefix = EduPrefix + "nav_";
        private const string PagePrefix = EduPrefix + "page_";

        private static readonly Dictionary<string, string> FolderEmojiMap = new()
{
    // --- Custom Category Emojis ---
    { "podcasts", "🎧" },
    { "risk_management", "🛡️" },
    { "video_tutorials", "🎬" },
    { "market_basics", "📈" },
    { "advanced_strategies", "🚀" },
    { "trader_psychology", "🧠" },

    // --- Language Flag Emojis ---
    // Tier 1
    { "en", "🇬🇧" }, { "es", "🇪🇸" }, { "ru", "🇷🇺" }, { "de", "🇩🇪" }, { "fr", "🇫🇷" }, { "zh", "🇨🇳" },
    // Tier 2
    { "pt", "🇵🇹" }, { "ar", "🇸🇦" }, { "ja", "🇯🇵" }, { "ko", "🇰🇷" }, { "hi", "🇮🇳" }, { "tr", "🇹🇷" }, { "fa", "🇮🇷" },
    // Tier 3
    { "it", "🇮🇹" }, { "nl", "🇳🇱" }, { "pl", "🇵🇱" }, { "id", "🇮🇩" }, { "vi", "🇻🇳" },
    // Tier 4 (From your previous list)
    { "sv", "🇸🇪" }, { "no", "🇳🇴" }, { "da", "🇩🇰" }, { "fi", "🇫🇮" }, { "uk", "🇺🇦" }, { "bg", "🇧🇬" },
    { "ms", "🇲🇾" }, { "th", "🇹🇭" }, { "he", "🇮🇱" }, { "el", "🇬🇷" }, { "ro", "🇷🇴" }, { "hu", "🇭🇺" },
    { "cs", "🇨🇿" }, { "sk", "🇸🇰" }, { "sl", "🇸🇮" }, { "hr", "🇭🇷" }, { "sr", "🇷🇸" }, { "lt", "🇱🇹" },
    { "lv", "🇱🇻" }, { "et", "🇪🇪" }, { "is", "🇮🇸" },
};
        private static readonly List<(string Code, string Name)> PrioritizedLanguages = new()
{
    // Tier 1: Major Global Reach
    ("en", "🇬🇧 English"),
    ("es", "🇪🇸 Español (Spanish)"),
    ("ru", "🇷🇺 Русский (Russian)"),
    ("de", "🇩🇪 Deutsch (German)"),
    ("fr", "🇫🇷 Français (French)"),
    ("zh", "🇨🇳 中文 (Chinese)"),
    
    // Tier 2: Significant Regional & Economic
    ("pt", "🇵🇹 Português (Portuguese)"),
    ("ar", "🇸🇦 العربية (Arabic)"),
    ("ja", "🇯🇵 日本語 (Japanese)"),
    ("ko", "🇰🇷 한국어 (Korean)"),
    ("hi", "🇮🇳 हिन्दी (Hindi)"),
    ("tr", "🇹🇷 Türkçe (Turkish)"),
    ("fa", "🇮🇷 فارسی (Persian)"),

    // Tier 3: European & Southeast Asian
    ("it", "🇮🇹 Italiano (Italian)"),
    ("nl", "🇳🇱 Nederlands (Dutch)"),
    ("pl", "🇵🇱 Polski (Polish)"),
    ("id", "🇮🇩 Bahasa Indonesia"),
    ("vi", "🇻🇳 Tiếng Việt (Vietnamese)"),
    ("ms", "🇲🇾 Bahasa Melayu (Malay)"),
    ("th", "🇹🇭 ภาษาไทย (Thai)"),

    // Tier 4: Other European
    ("sv", "🇸🇪 Svenska (Swedish)"),
    ("no", "🇳🇴 Norsk (Norwegian)"),
    ("da", "🇩🇰 Dansk (Danish)"),
    ("fi", "🇫🇮 Suomi (Finnish)"),
    ("uk", "🇺🇦 Українська (Ukrainian)"),
    ("ro", "🇷🇴 Română (Romanian)"),
    ("hu", "🇭🇺 Magyar (Hungarian)"),
    ("cs", "🇨🇿 Čeština (Czech)"),
    ("el", "🇬🇷 Ελληνικά (Greek)"),
    ("he", "🇮🇱 עברית (Hebrew)"),

    // And more...
};
        #endregion

        public EducationalContentHandler(
            ILogger<EducationalContentHandler> logger,
            ITelegramMessageSender messageSender,
            ITelegramBotClient botClient,
            IMemoryCacheService<CachedMenuDto> cache) // <-- FIX: Use the new DTO
        {
            _logger = logger;
            _messageSender = messageSender;
            _botClient = botClient;
            _cache = cache;

            InitializeContentDirectories();
        }

        private void InitializeContentDirectories()
        {
            _logger.LogInformation("Verifying educational content directory structure at '{BasePath}'...", BaseLearningPath);
            try
            {
                var contentCategories = new[] { "podcasts", "risk_management", "video_tutorials" };
                var languagesToSupport = PrioritizedLanguages.Select(lang => lang.Code);

                foreach (var category in contentCategories)
                {
                    foreach (var langCode in languagesToSupport)
                    {
                        Directory.CreateDirectory(Path.Combine(BaseLearningPath, category, langCode));
                    }
                }
                _logger.LogInformation("Content directory structure verified successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "FATAL: Could not create educational content directories. Check permissions for '{Path}'.", BaseLearningPath);
            }
        }

        public bool CanHandle(Update update) => update.Type == UpdateType.CallbackQuery && update.CallbackQuery?.Data?.StartsWith(EduPrefix) == true;

        public async Task HandleAsync(Update update, CancellationToken cancellationToken = default)
        {
            var callbackQuery = update.CallbackQuery!;
            await _messageSender.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);

            var chatId = callbackQuery.Message!.Chat.Id;
            var messageId = callbackQuery.Message.MessageId;
            var data = callbackQuery.Data!;

            using var logScope = _logger.BeginScope("EduHandler: User={UserId}, CbData={Data}", callbackQuery.From.Id, data);

            string relativePath;
            int page = 1;

            if (data.StartsWith(PagePrefix))
            {
                // e.g., edu_page_podcasts/en_page_2
                var parts = data.Substring(PagePrefix.Length).Split(new[] { "_page_" }, StringSplitOptions.None);
                relativePath = parts[0];
                page = int.Parse(parts[1]);
            }
            else
            {
                relativePath = data.Substring(NavPrefix.Length);
            }

            if (relativePath.Contains("..") || Path.IsPathRooted(relativePath))
            {
                _logger.LogWarning("Directory traversal attempt blocked: '{Path}'", relativePath);
                return;
            }

            var absolutePath = Path.GetFullPath(Path.Combine(BaseLearningPath, relativePath));

            if (File.Exists(absolutePath))
                await SendFileAsync(chatId, absolutePath, cancellationToken);
            else if (Directory.Exists(absolutePath))
                await ShowFolderContentsAsync(chatId, messageId, relativePath, page, cancellationToken);
            else
                _logger.LogWarning("User requested a path that does not exist: {Path}", absolutePath);
        }

        private async Task ShowFolderContentsAsync(long chatId, int messageId, string relativePath, int page, CancellationToken ct)
        {
            var cacheKey = $"edu_menu_{relativePath}_p{page}";
            if (!_cache.TryGetValue(cacheKey, out var cachedMenu))
            {
                _logger.LogInformation("CACHE MISS: Generating menu for path '{Path}', page {Page}.", relativePath, page);
                cachedMenu = GenerateFolderMenu(relativePath, page);
                _cache.Set(cacheKey, cachedMenu, CacheDuration);
            }
            else
            {
                _logger.LogInformation("CACHE HIT: Serving menu for path '{Path}', page {Page}.", relativePath, page);
            }

            // --- FIX: Correct argument order ---
            await _messageSender.EditMessageTextAsync(chatId, messageId, cachedMenu.Text, ParseMode.Markdown, cachedMenu.Keyboard, ct);
        }

        private CachedMenuDto GenerateFolderMenu(string relativePath, int page)
        {
            var absolutePath = Path.Combine(BaseLearningPath, relativePath);
            var currentFolderName = Path.GetFileName(relativePath);

            // --- 1. Generate a Smart, Context-Aware Title ---
            string title;
            if (string.IsNullOrEmpty(relativePath))
            {
                title = "📚 Learning Center";
            }
            else
            {
                // Create a breadcrumb trail for navigation, e.g., "Podcasts > English > Market Basics"
                var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Select(part => $"{GetEmojiForName(part)} {GetFormattedName(part)}");
                title = string.Join(" > ", pathParts);
            }

            // --- 2. Generate a Better Prompt ---
            var prompt = "Please choose a category or file below:";

            var allItems = new List<InlineKeyboardButton>();

            // Get sub-directories
            var directories = Directory.GetDirectories(absolutePath).OrderBy(d => d);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                var newRelativePath = Path.Combine(relativePath, dirName).Replace('\\', '/');

                // --- 3. Use Full Language Names for Language Folders ---
                string buttonText;
                if (PrioritizedLanguages.Any(l => l.Code == dirName))
                {
                    // It's a language folder, use the full name from our list
                    buttonText = PrioritizedLanguages.First(l => l.Code == dirName).Name;
                }
                else
                {
                    // It's a regular category folder
                    buttonText = $"{GetEmojiForName(dirName)} {GetFormattedName(dirName)}";
                }

                allItems.Add(InlineKeyboardButton.WithCallbackData(buttonText, NavPrefix + newRelativePath));
            }

            // Get files
            var files = Directory.GetFiles(absolutePath)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f);

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var newRelativePath = Path.Combine(relativePath, fileName).Replace('\\', '/');
                allItems.Add(InlineKeyboardButton.WithCallbackData($"▶️ {GetFormattedName(fileName)}", NavPrefix + newRelativePath));
            }

            // --- Pagination Logic ---
            var totalItems = allItems.Count;
            var totalPages = (int)Math.Ceiling(totalItems / (double)PageSize);
            page = Math.Clamp(page, 1, totalPages);
            var itemsForPage = allItems.Skip((page - 1) * PageSize).Take(PageSize).ToList();

            var finalTitle = totalPages > 1
                ? $"{title} (Page {page}/{totalPages})"
                : title;

            var textBuilder = new StringBuilder();
            textBuilder.AppendLine(TelegramMessageFormatter.Bold(finalTitle));
            textBuilder.AppendLine(); // Add spacing
            textBuilder.AppendLine(TelegramMessageFormatter.Italic(prompt));

            if (totalItems == 0)
            {
                textBuilder.AppendLine();
                textBuilder.AppendLine("_This section is currently empty._");
            }

            var keyboardRows = itemsForPage.Select(b => new List<InlineKeyboardButton> { b }).ToList();

            // Navigation Buttons
            var navButtons = new List<InlineKeyboardButton>();
            if (page > 1) navButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️ Previous", $"{PagePrefix}{relativePath}_page_{page - 1}"));
            if (page < totalPages) navButtons.Add(InlineKeyboardButton.WithCallbackData("Next ➡️", $"{PagePrefix}{relativePath}_page_{page + 1}"));
            if (navButtons.Any()) keyboardRows.Add(navButtons);

            var parentRelativePath = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');
            if (parentRelativePath != null)
            {
                keyboardRows.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData("⬆️ Up One Level", NavPrefix + parentRelativePath) });
            }

            // Always provide a way back to the start of the educational menu
            if (!string.IsNullOrEmpty(relativePath))
            {
                keyboardRows.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData("↩️ Learning Center", NavPrefix) });
            }

            return new CachedMenuDto(textBuilder.ToString(), new InlineKeyboardMarkup(keyboardRows));
        }

        private async Task SendFileAsync(long chatId, string absolutePath, CancellationToken ct)
        {
            _logger.LogInformation("User requested file: {FilePath}", absolutePath);
            await _botClient.SendChatAction(chatId, ChatAction.UploadDocument);

            var fileName = Path.GetFileName(absolutePath);
            await using var fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var inputFile = InputFile.FromStream(fileStream, fileName);

            try
            {
                var caption = GetFormattedName(fileName);
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                if (extension is ".mp3" or ".wav")
                    await _botClient.SendAudio(chatId, inputFile, caption: caption, cancellationToken: ct);
                else if (extension is ".mp4" or ".mov" or ".mkv")
                    await _botClient.SendVideo(chatId, inputFile, caption: caption, cancellationToken: ct);
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to send file {FileName} to chat {ChatId}", fileName, chatId); }
        }

        private static string GetFormattedName(string name) => string.IsNullOrEmpty(name) ? "Back" : Path.GetFileNameWithoutExtension(name).Replace("_", " ").Replace("-", " ");
        private static string GetEmojiForName(string name) => FolderEmojiMap.TryGetValue(name.ToLowerInvariant(), out var emoji) ? emoji : "📁";
    }
}