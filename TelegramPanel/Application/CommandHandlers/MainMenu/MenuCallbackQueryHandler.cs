// File: TelegramPanel/Application/CommandHandlers/MenuCallbackQueryHandler.cs
#region Usings
using Application.Interfaces;
using AutoMapper;
using Microsoft.Extensions.Logging;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramPanel.Application.CommandHandlers.Settings;
using TelegramPanel.Application.Interfaces;
using TelegramPanel.Application.States;
using TelegramPanel.Formatters;
using TelegramPanel.Infrastructure.Helper;
using static TelegramPanel.Infrastructure.ActualTelegramMessageActions;
#endregion

namespace TelegramPanel.Application.CommandHandlers.MainMenu
{
    public class MenuCallbackQueryHandler : ITelegramCommandHandler, ITelegramCallbackQueryHandler
    {
        #region Private Fields
        private readonly ILogger<MenuCallbackQueryHandler> _logger;
        private readonly ITelegramMessageSender _messageSender;
        private readonly ITelegramBotClient _botClient;
        private readonly IUserService _userService;
        private readonly ISignalService _signalService;
        private readonly IMapper _mapper;
        private readonly IPaymentService _paymentService; //✅ تزریق سرویس پرداخت
        private readonly IUserConversationStateService _stateService; // Inject the state service
        public const string BackToMainMenuGeneral = "main_menu_back"; // ✅ این ثابت تعریف شد
        private readonly ICryptoPriceService _cryptoPriceService;
        // Callback Data Prefix برای انتخاب پلن
        public const string SelectPlanPrefix = "select_plan_";

        // Callback Data Prefix برای انتخاب ارز و نهایی کردن پرداخت
        public const string PayWithCryptoPrefix = "pay_";

        // شناسه پلن‌ها
        private static readonly Guid PremiumMonthlyPlanId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        private static readonly Guid PremiumQuarterlyPlanId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        // private readonly MenuCommandHandler _menuCommandHandler; // ✅ برای فراخوانی مستقیم (روش جایگزین)
        #endregion

        // Callback Data constants برای دکمه‌های بازگشت
        public const string BackToMainMenuFromProfile = "main_menu_from_profile";
        public const string BackToMainMenuFromSubscribe = "main_menu_from_subscribe";
        public const string BackToMainMenuFromSettings = "main_menu_from_settings";
        // می‌توانید یک CallbackData عمومی برای بازگشت به منو در نظر بگیرید
        public const string GeneralBackToMainMenuCallback = "main_menu_back";
        // Callback data برای انتخاب پلن‌ها
        public const string SelectPlanPremiumMonthly = "select_plan_premium_1m";
        public const string SelectPlanPremiumQuarterly = "select_plan_premium_3m";

        // Callback data برای انتخاب ارز دیجیتال برای پرداخت
        public const string PayWithUsdtForPremiumMonthly = "pay_usdt_premium_1m";

        private const decimal PremiumPlanPriceUsd = 150.00m;
        private const decimal BestPlanPriceUsd = 500.00m;

        private static readonly Dictionary<string, string> CryptoAssetToCoinGeckoId = new()
        {
            { "USDT", "tether" },
            { "TON", "the-open-network" },
            { "BTC", "bitcoin" }
        };

        #region Constructor
        public MenuCallbackQueryHandler(ICryptoPriceService cryptoPriceService,
            ILogger<MenuCallbackQueryHandler> logger,
            ITelegramMessageSender messageSender,
            ITelegramBotClient botClient,
            IUserService userService,
            ISignalService signalService,
            IMapper mapper,
            IPaymentService paymentService
            // MenuCommandHandler menuCommandHandler // ✅ تزریق اگر می‌خواهید مستقیم فراخوانی کنید,
            , IUserConversationStateService stateService
            )
        {
            _cryptoPriceService = cryptoPriceService ?? throw new ArgumentNullException(nameof(cryptoPriceService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messageSender = messageSender ?? throw new ArgumentNullException(nameof(messageSender));
            _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _signalService = signalService ?? throw new ArgumentNullException(nameof(signalService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService)); // Initialize the state service
            // _menuCommandHandler = menuCommandHandler; // ✅ مقداردهی
        }
        #endregion

        #region ITelegramCommandHandler Implementation
        public bool CanHandle(Update update)
        {
            if (update.Type != UpdateType.CallbackQuery || update.CallbackQuery?.Data == null)
            {
                return false;
            }

            string callbackData = update.CallbackQuery.Data;

            _logger.LogTrace("MenuCBQHandler.CanHandle: Checking callbackData '{CallbackData}' against known prefixes.", callbackData);

            bool canHandleIt =
                callbackData.Equals(MenuCommandHandler.SignalsCallbackData, StringComparison.Ordinal) || // "menu_view_signals"
                callbackData.Equals(MenuCommandHandler.ProfileCallbackData, StringComparison.Ordinal) || // "menu_my_profile"
                callbackData.Equals(MenuCommandHandler.SubscribeCallbackData, StringComparison.Ordinal) || // "menu_subscribe_plans"
                callbackData.Equals(MenuCommandHandler.SettingsCallbackData, StringComparison.Ordinal) || // "menu_user_settings"
                 callbackData.Equals(MenuCommandHandler.AnalysisCallbackData, StringComparison.Ordinal) || // <<< NEW: Handle analysis button

                callbackData.StartsWith("select_plan_", StringComparison.Ordinal) ||
                callbackData.StartsWith("pay_", StringComparison.Ordinal) ||

                callbackData.Equals(BackToMainMenuFromProfile, StringComparison.Ordinal) ||
                callbackData.Equals(BackToMainMenuFromSubscribe, StringComparison.Ordinal) ||
                callbackData.Equals(BackToMainMenuFromSettings, StringComparison.Ordinal) ||
                callbackData.StartsWith(PayWithCryptoPrefix, StringComparison.Ordinal) ||

                callbackData.Equals(MenuCommandHandler.BackToMainMenuGeneral, StringComparison.Ordinal);

            _logger.LogTrace("MenuCBQHandler.CanHandle for '{CallbackData}': Result = {Result}", callbackData, canHandleIt);
            return canHandleIt;
        }

        // In MenuCallbackQueryHandler.cs
        public async Task HandleAsync(Update update, CancellationToken cancellationToken = default)
        {
            CallbackQuery? callbackQuery = update.CallbackQuery;
            if (callbackQuery?.Message?.Chat == null || callbackQuery.From == null || string.IsNullOrWhiteSpace(callbackQuery.Data))
            {
                _logger.LogWarning("MenuCallbackHandler: CallbackQuery, its Message, Chat, From user, or Data is null/empty in UpdateID {UpdateId}.", update.Id);
                if (callbackQuery != null)
                {
                    await AnswerCallbackQuerySilentAsync(callbackQuery.Id, cancellationToken, "Error processing request.");
                }
                return;
            }

            long chatId = callbackQuery.Message.Chat.Id;
            long userId = callbackQuery.From.Id;
            int messageId = callbackQuery.Message.MessageId;
            string callbackData = callbackQuery.Data;

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["TelegramUserId"] = userId,
                ["ChatId"] = chatId,
                ["CallbackData"] = callbackData,
                ["MessageId"] = messageId
            }))
            {
                _logger.LogInformation("Handling CallbackQuery.");
                await AnswerCallbackQuerySilentAsync(callbackQuery.Id, cancellationToken, "Processing...");

                try
                {
                    if (callbackData.StartsWith(PayWithCryptoPrefix))
                    {
                        await HandleCryptoPaymentSelectionAsync(chatId, userId, messageId, callbackData, cancellationToken);
                    }
                    else if (callbackData.StartsWith(SelectPlanPrefix))
                    {
                        await HandlePlanSelectionAsync(chatId, userId, messageId, callbackData, cancellationToken);
                    }
                    else
                    {
                        switch (callbackData)
                        {
                            case MenuCommandHandler.SignalsCallbackData:
                                await HandleViewSignalsAsync(chatId, userId, messageId, cancellationToken);
                                break;
                            case MenuCommandHandler.ProfileCallbackData:
                                await HandleMyProfileAsync(chatId, userId, messageId, cancellationToken);
                                break;
                            case MenuCommandHandler.SubscribeCallbackData:
                                await ShowSubscriptionPlansAsync(chatId, messageId, cancellationToken);
                                break;
                            case MenuCommandHandler.SettingsCallbackData:
                                await HandleSettingsAsync(chatId, userId, messageId, cancellationToken);
                                break;
                            case MenuCommandHandler.AnalysisCallbackData:
                                await ShowAnalysisMenuAsync(chatId, messageId, cancellationToken);
                                break;

                            // =================== THIS IS THE FIX ===================
                            // The case now checks against the correct constant from the correct file.
                            case MenuCommandHandler.BackToMainMenuGeneral:
                                // =======================================================
                                _logger.LogInformation("User requested to go back to main menu.");
                                await ShowMainMenuAndClearStateAsync(chatId, userId, messageId, cancellationToken);
                                break;

                            default:
                                _logger.LogWarning("Unhandled CallbackQuery data: {CallbackData}", callbackData);
                                await _messageSender.SendTextMessageAsync(chatId, "Sorry, this option is not recognized or is under development.", cancellationToken: cancellationToken);
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while handling callback query data '{CallbackData}'.", callbackData);
                    await _messageSender.SendTextMessageAsync(chatId, "An unexpected error occurred while processing your request. Please try again or contact support.", cancellationToken: cancellationToken);
                }
            }
        }

        #endregion

        // VVVVVV NEW METHOD VVVVVV
        /// <summary>
        /// Displays the news analysis sub-menu.
        /// </summary>
          // VVVVVV MODIFIED METHOD VVVVVV
        /// <summary>
        /// Displays the news analysis sub-menu.
        /// </summary>
        private async Task ShowAnalysisMenuAsync(long chatId, int messageIdToEdit, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Showing news analysis menu to ChatID {ChatId}", chatId);

            string text = TelegramMessageFormatter.Bold("🔍 News Analysis Tools") + "\n\n" +
                       "Select a tool to analyze news content from our indexed sources:";

            InlineKeyboardMarkup? keyboard = MarkupBuilder.CreateInlineKeyboard(
                 new[]
                {
                    // VVVVVV NEW BUTTON VVVVVV
                    InlineKeyboardButton.WithCallbackData("📊 Market Sentiment", "analysis_sentiment")
                },
                new[]
                { 
                    // New "Central Bank Watch" button added
                    InlineKeyboardButton.WithCallbackData("🏛️ Central Bank Watch", "analysis_cb_watch")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔎 Search by Keywords", "analysis_search_keywords")
                },
                // new[] 
                // { 
                //    InlineKeyboardButton.WithCallbackData("📚 Search by Source", "analysis_search_source") // Future feature
                // },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Back to Main Menu", BackToMainMenuGeneral)
                }
            );

            await EditMessageOrSendNewAsync(chatId, messageIdToEdit, text, keyboard, ParseMode.Markdown, cancellationToken);
        }

        private async Task HandlePlanSelectionAsync(long chatId, long telegramUserId, int messageIdToEdit, string callbackData, CancellationToken cancellationToken)
        {
            _logger.LogInformation("UserID {TelegramUserId} selected a plan. CallbackData: {CallbackData}", telegramUserId, callbackData);
            string planIdString = callbackData[SelectPlanPrefix.Length..];
            if (!Guid.TryParse(planIdString, out Guid selectedPlanId))
            {
                await EditMessageOrSendNewAsync(chatId, messageIdToEdit, "Invalid plan selection.", null, ParseMode.Markdown, cancellationToken);
                return;
            }

            // ✅ MODIFIED: Update display name to match new plan names
            string planNameForDisplay = selectedPlanId == PremiumMonthlyPlanId ? $"Premium Plan (${PremiumPlanPriceUsd})" :
                                        selectedPlanId == PremiumQuarterlyPlanId ? $"Best Plan (${BestPlanPriceUsd})" :
                                        "Selected Plan";

            string paymentOptionsText = $"You have selected: {TelegramMessageFormatter.Bold(planNameForDisplay)}.\n\n" +
                                        "Please choose your preferred cryptocurrency for payment:";

            InlineKeyboardMarkup? paymentKeyboard = MarkupBuilder.CreateInlineKeyboard(
                new[] { InlineKeyboardButton.WithCallbackData("💳 Pay with USDT", $"{PayWithCryptoPrefix}usdt_for_plan_{selectedPlanId}") },
                new[] { InlineKeyboardButton.WithCallbackData("💳 Pay with TON", $"{PayWithCryptoPrefix}ton_for_plan_{selectedPlanId}") },
                new[] { InlineKeyboardButton.WithCallbackData("💳 Pay with BTC", $"{PayWithCryptoPrefix}btc_for_plan_{selectedPlanId}") },
                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Change Plan", MenuCommandHandler.SubscribeCallbackData) }
            );

            await EditMessageOrSendNewAsync(chatId, messageIdToEdit, paymentOptionsText, paymentKeyboard, ParseMode.Markdown, cancellationToken);
        }



        private async Task HandleCryptoPaymentSelectionAsync(long chatId, long telegramUserId, int messageIdToEdit, string callbackData, CancellationToken cancellationToken)
        {
            _logger.LogInformation("UserID {TelegramUserId} selected crypto payment. Callback: {CallbackData}", telegramUserId, callbackData);

            // --- 1. Parse Callback Data ---
            string[] parts = callbackData[PayWithCryptoPrefix.Length..].Split(new[] { "_for_plan_" }, StringSplitOptions.None);
            if (parts.Length != 2 || !Guid.TryParse(parts[1], out Guid selectedPlanId))
            {
                await EditMessageOrSendNewAsync(chatId, messageIdToEdit, "Invalid payment option. Please try again.", null, ParseMode.Markdown, cancellationToken);
                return;
            }
            string selectedCryptoAsset = parts[0].ToUpper();

            await EditMessageOrSendNewAsync(chatId, messageIdToEdit, $"⏳ Please wait, calculating price and generating invoice for {selectedCryptoAsset}...", null, ParseMode.Markdown, cancellationToken);

            // --- 2. Determine Plan's USD Price ---
            decimal usdPrice = selectedPlanId == PremiumMonthlyPlanId ? PremiumPlanPriceUsd :
                               selectedPlanId == PremiumQuarterlyPlanId ? BestPlanPriceUsd : 0;

            if (usdPrice <= 0)
            {
                await _messageSender.SendTextMessageAsync(chatId, "Error: Invalid plan selected.", cancellationToken: cancellationToken);
                return;
            }

            // --- 3. Get Live Crypto Price ---
            if (!CryptoAssetToCoinGeckoId.TryGetValue(selectedCryptoAsset, out var coinGeckoId))
            {
                await _messageSender.SendTextMessageAsync(chatId, $"Error: The cryptocurrency '{selectedCryptoAsset}' is not supported.", cancellationToken: cancellationToken);
                return;
            }

            var prices = await _cryptoPriceService.GetPricesAsync(new[] { coinGeckoId }, "usd");
            if (prices == null || !prices.TryGetValue(coinGeckoId, out decimal liveCryptoPrice) || liveCryptoPrice <= 0)
            {
                await _messageSender.SendTextMessageAsync(chatId, $"⚠️ Sorry, we couldn't fetch the live price for {selectedCryptoAsset}. Please try again in a moment.", cancellationToken: cancellationToken);
                return;
            }

            // --- 4. Calculate Final Crypto Amount ---
            decimal finalCryptoAmount = usdPrice / liveCryptoPrice;
            _logger.LogInformation("Calculation: Plan USD ${UsdPrice} / {Asset} Price ${LivePrice} = {FinalAmount} {Asset}",
                usdPrice, selectedCryptoAsset, liveCryptoPrice, finalCryptoAmount, selectedCryptoAsset);

            // --- 5. Call the Payment Service with the dynamic amount ---
            var userDto = await _userService.GetUserByTelegramIdAsync(telegramUserId.ToString(), cancellationToken);
            if (userDto == null)
            {
                await _messageSender.SendTextMessageAsync(chatId, "Error: Your user profile could not be found.", cancellationToken: cancellationToken);
                return;
            }

            // ✅ CALLING THE NEW, CORRECTED METHOD
            var invoiceResult = await _paymentService.CreateCryptoPaymentInvoiceAsync(
                userDto.Id,
                selectedPlanId,
                selectedCryptoAsset,
                finalCryptoAmount, // Pass the dynamically calculated crypto amount
                cancellationToken
            );

            // --- 6. Display Result to User (same as before) ---
            if (invoiceResult.Succeeded && invoiceResult.Data != null)
            {
                var invoice = invoiceResult.Data;
                string paymentMessage = $"✅ Your payment invoice for {TelegramMessageFormatter.Bold(finalCryptoAmount.ToString("F8") + " " + selectedCryptoAsset, escapePlainText: false)} has been created!\n\n" +
                                     // ... rest of the success message
                                     $"Please use the button below or copy the link to complete your payment.\n\n" +
                                     $"{TelegramMessageFormatter.Link("➡️ Click here to Pay ⬅️", invoice.BotInvoiceUrl!)}";

                InlineKeyboardMarkup? paymentLinkKeyboard = MarkupBuilder.CreateInlineKeyboard(
                    new[] { InlineKeyboardButton.WithUrl($"🚀 Pay with {selectedCryptoAsset} Now", invoice.BotInvoiceUrl!) },
                    new[] { InlineKeyboardButton.WithCallbackData("⬅️ Back to Main Menu", BackToMainMenuGeneral) }
                );

                await _botClient.SendMessage(chatId, paymentMessage, ParseMode.Markdown, replyMarkup: paymentLinkKeyboard, cancellationToken: cancellationToken);
                // You can now delete the "Please wait" message
                await _botClient.DeleteMessage(chatId, messageIdToEdit, cancellationToken);
            }
            else
            {
                string failureMessage = $"⚠️ Sorry, we couldn't create your payment invoice for {selectedCryptoAsset}.\n" +
                                     $"Details: {string.Join("; ", invoiceResult.Errors)}\n\n" +
                                     "Please try a different payment method or contact support.";
                await _botClient.EditMessageText(chatId, messageIdToEdit, failureMessage, cancellationToken: cancellationToken);
                // Optionally, show the plans again after a delay or with a button
            }
        }

        #region Private Handler Methods for Callbacks

        // Helper method to format crypto prices
        // Helper method to format crypto prices
        private string FormatCryptoPrices(decimal usdAmount, decimal btcPrice, decimal usdtPrice, decimal tonPrice)
        {
            var priceBuilder = new StringBuilder();
            priceBuilder.Append($"💰 Your Investment: {TelegramMessageFormatter.Bold($"${usdAmount:F2} USD", escapePlainText: false)}");

            if (usdtPrice > 0) priceBuilder.Append($"\n    ~ {usdAmount / usdtPrice:F2} USDT");
            if (tonPrice > 0) priceBuilder.Append($" | ~ {usdAmount / tonPrice:F2} TON");
            if (btcPrice > 0) priceBuilder.Append($" | ~ {usdAmount / btcPrice:F6} BTC");

            return priceBuilder.ToString();
        }

        private async Task ShowSubscriptionPlansAsync(long chatId, int messageIdToEdit, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Showing visually enhanced subscription plans to ChatID {ChatId}.", chatId);

            var planTextBuilder = new StringBuilder();
            planTextBuilder.AppendLine(TelegramMessageFormatter.Bold("🚀 Ready to Transform Your Trading? 🚀", escapePlainText: false));
            planTextBuilder.AppendLine("Unlock a new level of market intelligence and profitability with our carefully crafted plans.");
            planTextBuilder.AppendLine("Join a community driven by success!");
            planTextBuilder.AppendLine();

            // --- Plan 1: Premium Plan ---
            string premiumPlanDisplay =
                $"1️⃣ {TelegramMessageFormatter.Bold("Premium Plan")} | 30 Days Access\n" +
                "────────────────────\n" +
                "✅ **Perfect for:** Emerging traders aiming to refine their strategy and gain a solid market understanding.\n\n" +
                "🌟 **Unlock Your Potential:**\n" +
                "    • 📈 **Precision Trading Signals:** Benefit from our proprietary algorithms delivering clear, actionable signals across diverse assets.\n" +
                "    • 🧠 **Instinctive Market Sentiment:** Tap into real-time sentiment data to anticipate market shifts and make informed decisions.\n" +
                "    • ⚡ **Immediate Action Alerts:** Stay ahead with instant notifications on market moves, so you never miss an opportunity.\n" +
                "    • 💬 **Dedicated Telegram Support:** Our expert team is ready to assist you, ensuring a smooth and productive experience.\n" +
                "    • 📚 **Foundational Trading Guides:** Build a strong knowledge base with practical resources designed for growth.\n" +
                "    • 📊 **Essential Market Overview:** Keep track of key price movements with our accessible charting tools.\n"; // Subtle mention

            // --- Plan 2: Best Plan ---
            string bestPlanDisplay =
                $"2️⃣ {TelegramMessageFormatter.Bold("Best Plan")} | 90 Days Access (Exceptional Value!)\n" +
                "────────────────────\n" +
                "✅ **Ideal for:** Serious traders and investors who demand a significant, consistent edge and accelerated growth.\n\n" +
                "🌟 **Achieve Peak Performance (All Premium Features PLUS):**\n" +
                "    • 🚀 **Elevated Trading Advantage:** All the power of Premium, supercharged.\n" +
                "    • 📊 **Deep-Dive Interactive Charting:** Explore markets with advanced charting, comprehensive technical indicators, and rich historical data. Visualize your success!\n" + // More descriptive of charting
                "    • 📰 **Exclusive Market Intelligence:** Gain access to our cutting-edge research reports and expert analysis for strategic advantage.\n" +
                "    • 🔔 **Intelligent Alert Customization:** Fine-tune your notifications precisely to your trading style and priorities.\n" +
                "    • 🏆 **Elite VIP Community Access:** Connect and collaborate with a network of high-caliber traders in our private Telegram group.\n" +
                "    • 🎁 **Generous Loyalty Rewards:** Your activity earns you points for exclusive discounts and premium access. We value your commitment!\n" + // Frame rewards as valuing commitment
                "    • 💡 **Curated Investment Opportunities:** Discover unique investment ideas and gain potential insights into portfolio optimization.\n" + // Sound exclusive
                "    • 💰 **Unlock ~10% Savings:** Lock in the best value by choosing our 90-day plan!"; // Stronger saving message

            string planSelectionPrompt = "Select the plan that propels your trading journey:";

            // Fetch live prices to make the display dynamic
            decimal btcPrice = 0, usdtPrice = 0, tonPrice = 0;
            bool pricesFetched = false;

            try
            {
                var prices = await _cryptoPriceService.GetPricesAsync(CryptoAssetToCoinGeckoId.Values, "usd");

                if (prices != null && prices.Any())
                {
                    prices.TryGetValue(CryptoAssetToCoinGeckoId["BTC"], out btcPrice);
                    prices.TryGetValue(CryptoAssetToCoinGeckoId["USDT"], out usdtPrice);
                    prices.TryGetValue(CryptoAssetToCoinGeckoId["TON"], out tonPrice);
                    pricesFetched = true;

                    _logger.LogInformation("Live prices: BTC=${BtcPrice}, USDT=${UsdtPrice}, TON=${TonPrice}", btcPrice, usdtPrice, tonPrice);

                    // --- Update Premium Plan details with dynamic pricing ---
                    premiumPlanDisplay =
                        $"1️⃣ {TelegramMessageFormatter.Bold("Premium Plan")} | 30 Days Access\n" +
                        "────────────────────\n" +
                        "✅ **Perfect for:** Emerging traders aiming to refine their strategy and gain a solid market understanding.\n\n" +
                        "🌟 **Unlock Your Potential:**\n" +
                        "    • 📈 **Precision Trading Signals:** Benefit from our proprietary algorithms delivering clear, actionable signals across diverse assets.\n" +
                        "    • 🧠 **Instinctive Market Sentiment:** Tap into real-time sentiment data to anticipate market shifts and make informed decisions.\n" +
                        "    • ⚡ **Immediate Action Alerts:** Stay ahead with instant notifications on market moves, so you never miss an opportunity.\n" +
                        "    • 💬 **Dedicated Telegram Support:** Our expert team is ready to assist you, ensuring a smooth and productive experience.\n" +
                        "    • 📚 **Foundational Trading Guides:** Build a strong knowledge base with practical resources designed for growth.\n" +
                        "    • 📊 **Essential Market Overview:** Keep track of key price movements with our accessible charting tools.\n" +
                        $"({FormatCryptoPrices(PremiumPlanPriceUsd, btcPrice, usdtPrice, tonPrice)})";

                    // --- Update Best Plan details with dynamic pricing ---
                    bestPlanDisplay =
                        $"2️⃣ {TelegramMessageFormatter.Bold("Best Plan")} | 90 Days Access (Exceptional Value!)\n" +
                        "────────────────────\n" +
                        "✅ **Ideal for:** Active traders, serious investors, and those seeking a significant, consistent edge and accelerated growth.\n\n" +
                        "🌟 **Achieve Peak Performance (All Premium Features PLUS):**\n" +
                        "    • 🚀 **Elevated Trading Advantage:** All the power of Premium, supercharged.\n" +
                        "    • 📊 **Deep-Dive Interactive Charting:** Explore markets with advanced charting, comprehensive technical indicators, and rich historical data. Visualize your success!\n" +
                        "    • 📰 **Exclusive Market Intelligence:** Gain access to our cutting-edge research reports and expert analysis for strategic advantage.\n" +
                        "    • 🔔 **Intelligent Alert Customization:** Fine-tune your notifications precisely to your trading style and priorities.\n" +
                        "    • 🏆 **Elite VIP Community Access:** Connect and collaborate with a network of high-caliber traders in our private Telegram group.\n" +
                        "    • 🎁 **Generous Loyalty Rewards:** Your activity earns you points for exclusive discounts and premium access. We value your commitment!\n" +
                        "    • 💡 **Curated Investment Opportunities:** Discover unique investment ideas and gain potential insights into portfolio optimization.\n" +
                        "    • 💰 **Unlock ~10% Savings:** Lock in the best value by choosing our 90-day plan!\n" +
                        $"({FormatCryptoPrices(BestPlanPriceUsd, btcPrice, usdtPrice, tonPrice)})";
                }
                else
                {
                    _logger.LogWarning("Crypto price API failed or returned no data. Displaying USD prices only.");
                    planSelectionPrompt = "Choose your subscription plan (Prices shown in USD):";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching crypto prices. Showing default plan text.");
                // Default text will be used automatically in case of any exception.
            }

            planTextBuilder.AppendLine(premiumPlanDisplay);
            planTextBuilder.AppendLine();
            planTextBuilder.AppendLine(bestPlanDisplay);
            planTextBuilder.AppendLine();
            planTextBuilder.Append(TelegramMessageFormatter.Bold(planSelectionPrompt, escapePlainText: false));

            // Construct the inline keyboard
            InlineKeyboardMarkup? plansKeyboard = MarkupBuilder.CreateInlineKeyboard(
                new[] {
            InlineKeyboardButton.WithCallbackData(
                pricesFetched ? $"🌟 Premium Plan (${PremiumPlanPriceUsd:F2})" : $"🌟 Premium Plan",
                $"{SelectPlanPrefix}{PremiumMonthlyPlanId}"
            )
                },
                new[] {
            InlineKeyboardButton.WithCallbackData(
                pricesFetched ? $"✨ Best Plan (${BestPlanPriceUsd:F2})" : $"✨ Best Plan",
                $"{SelectPlanPrefix}{PremiumQuarterlyPlanId}"
            )
                },
                new[] {
            InlineKeyboardButton.WithCallbackData("⬅️ Back to Main Menu", BackToMainMenuGeneral)
                }
            );

            await EditMessageOrSendNewAsync(chatId, messageIdToEdit, planTextBuilder.ToString(), plansKeyboard, ParseMode.Markdown, cancellationToken);
        }


        // ... (متدهای HandleViewSignalsAsync, HandleMyProfileAsync, HandleSubscribeAsync, HandleSettingsAsync بدون تغییر عمده) ...
        // فقط متن UI را بهبود می‌دهیم و از دکمه بازگشت عمومی استفاده می‌کنیم

        private async Task HandleViewSignalsAsync(long chatId, long telegramUserId, int messageIdToEdit, CancellationToken cancellationToken)
        {
            _logger.LogInformation("User {TelegramUserId} requested to view signals.", telegramUserId);
            IEnumerable<global::Application.DTOs.SignalDto> signals = await _signalService.GetRecentSignalsAsync(3, includeCategory: true, cancellationToken: cancellationToken); //  تعداد سیگنال‌ها کمتر برای نمایش بهتر
            StringBuilder sb = new();

            if (signals.Any())
            {
                _ = sb.AppendLine(TelegramMessageFormatter.Bold("📊 Recent Trading Signals:"));
                _ = sb.AppendLine(); // Add a blank line for better readability
                foreach (global::Application.DTOs.SignalDto signalDto in signals)
                {
                    string formattedSignal = SignalFormatter.FormatSignal(signalDto, ParseMode.Markdown);
                    _ = sb.AppendLine(formattedSignal);
                    _ = sb.AppendLine("─".PadRight(20, '─')); // Separator line
                }
            }
            else
            {
                _ = sb.AppendLine("No active signals available at the moment. Please check back later!");
            }

            InlineKeyboardMarkup? backKeyboard = MarkupBuilder.CreateInlineKeyboard(
        InlineKeyboardButton.WithCallbackData("⬅️ Back to Main Menu", BackToMainMenuGeneral)
        );

            await EditMessageOrSendNewAsync(chatId, messageIdToEdit, sb.ToString(), backKeyboard, ParseMode.Markdown, cancellationToken);
        }

        /// <summary>
        /// Handles the "Back to Main Menu" callback data.
        /// Shows the main menu and clears the user's conversation state.
        /// </summary>
        private async Task ShowMainMenuAndClearStateAsync(long chatId, long userId, int messageIdToEdit, CancellationToken cancellationToken)
        {
            _logger.LogInformation("User requested to go back to main menu. Clearing state for UserID {UserId}.", userId);
            await ShowMainMenuAsync(chatId, messageIdToEdit, cancellationToken);
            await _stateService.ClearAsync(userId, cancellationToken); // Clear the user's state
        }
        // مثال برای ShowSubscriptionPlansAsync




        private async Task HandleMyProfileAsync(long chatId, long telegramUserId, int messageIdToEdit, CancellationToken cancellationToken)
        {
            _logger.LogInformation("User {TelegramUserId} requested to view profile.", telegramUserId);
            global::Application.DTOs.UserDto? userDto = await _userService.GetUserByTelegramIdAsync(telegramUserId.ToString(), cancellationToken);

            if (userDto == null)
            {
                await EditMessageOrSendNewAsync(chatId, messageIdToEdit, "Your profile could not be retrieved. Please try using /start again.", null, cancellationToken: cancellationToken);
                return;
            }

            StringBuilder sb = new();
            _ = sb.AppendLine(TelegramMessageFormatter.Bold("🔐 Your Profile:"));
            _ = sb.AppendLine($"👤 Username: {TelegramMessageFormatter.Code(userDto.Username)}");
            _ = sb.AppendLine($"📧 Email: {TelegramMessageFormatter.Code(userDto.Email)}");
            _ = sb.AppendLine($"🆔 Telegram ID: {TelegramMessageFormatter.Code(userDto.TelegramId)}");
            _ = sb.AppendLine($"⭐ Access Level: {TelegramMessageFormatter.Bold(GetLevelTitle((int)userDto.Level))}");
            _ = sb.AppendLine($"💰 Token Balance: {TelegramMessageFormatter.Code(userDto.TokenBalance.ToString("N2"))}");

            // تابع داخلی برای نمایش سطح دسترسی بدون نیاز به enum خارجی
            static string GetLevelTitle(int level)
            {
                return level switch
                {
                    0 => "🟢 Free",
                    1 => "🥉 Bronze",
                    2 => "🥈 Silver",
                    3 => "🥇 Gold",
                    4 => "💎 Platinum",
                    100 => "🛠️ Admin",
                    _ when level > 100 => $"👑 Custom ({level})",
                    _ => $"❓ Unknown ({level})"
                };
            }

            if (userDto.TokenWallet != null)
            {
                _ = sb.AppendLine($"Token Balance: {TelegramMessageFormatter.Code(userDto.TokenWallet.Balance.ToString("N2"))} Tokens");
            }
            if (userDto.ActiveSubscription != null)
            {
                _ = sb.AppendLine($"Active Subscription: Plan XXX (Expires: {userDto.ActiveSubscription.EndDate:yyyy-MM-dd})"); // نام پلن را اضافه کنید
            }
            else
            {
                _ = sb.AppendLine("Subscription: No active subscription.");
            }

            InlineKeyboardMarkup? backKeyboard = MarkupBuilder.CreateInlineKeyboard(
     InlineKeyboardButton.WithCallbackData("⬅️ Back to Main Menu", BackToMainMenuGeneral));
            await EditMessageOrSendNewAsync(chatId, messageIdToEdit, sb.ToString(), backKeyboard, ParseMode.MarkdownV2, cancellationToken);
        }



        /// <summary>
        /// Handles the "Settings" button callback from the main menu.
        /// It should display the main settings menu to the user.
        /// This method essentially replicates what SettingsCommandHandler does for the /settings command.
        /// </summary>
        private async Task HandleSettingsAsync(long chatId, long telegramUserId, int messageIdToEdit, CancellationToken cancellationToken)
        {
            _logger.LogInformation("UserID {TelegramUserId} in ChatID {ChatId} selected 'Settings' from main menu (via callback). Displaying settings menu.", telegramUserId, chatId);

            //  متن و دکمه‌های این بخش باید دقیقاً مشابه چیزی باشد که
            //  SettingsCommandHandler برای دستور /settings نمایش می‌دهد.
            //  برای جلوگیری از تکرار کد، می‌توانید این متن و کیبورد را در یک متد کمکی
            //  در یک کلاس جداگانه (مثلاً یک SettingsMenuBuilder) یا حتی در خود SettingsCommandHandler
            //  (به صورت یک متد استاتیک یا یک متد در یک سرویس که هر دو Handler به آن دسترسی دارند) تعریف کنید.

            //  فعلاً، منطق را مستقیماً اینجا بازنویسی می‌کنیم:
            string settingsMenuText = TelegramMessageFormatter.Bold("⚙️ User Settings", escapePlainText: false) + "\n\n" +
                                   "Please choose a category to configure:";

            // دکمه‌های منوی تنظیمات (اینها باید با ثابت‌های CallbackData در SettingsCommandHandler و SettingsCallbackQueryHandler مطابقت داشته باشند)
            InlineKeyboardMarkup? settingsKeyboard = MarkupBuilder.CreateInlineKeyboard(
     new[] { InlineKeyboardButton.WithCallbackData("📊 My Signal Preferences", SettingsCommandHandler.PrefsSignalCategoriesCallback) },
     new[] { InlineKeyboardButton.WithCallbackData("🔔 Notification Settings", SettingsCommandHandler.PrefsNotificationsCallback) },
     new[] { InlineKeyboardButton.WithCallbackData("⭐ My Subscription", SettingsCommandHandler.MySubscriptionInfoCallback) },
     new[] { InlineKeyboardButton.WithCallbackData("⬅️ Back to Main Menu", BackToMainMenuGeneral) });

            // ویرایش پیام قبلی (که دکمه‌های منوی اصلی را داشت) با منوی تنظیمات جدید
            await EditMessageOrSendNewAsync(
                chatId: chatId,
                messageId: messageIdToEdit,
                text: settingsMenuText,
                replyMarkup: settingsKeyboard,
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: cancellationToken);
        }

        // ... (بقیه متدها مانند ShowMainMenuAsync, EditMessageOrSendNewAsync, AnswerCallbackQuerySilentAsync) ...


        #endregion


        /// <summary>
        /// منوی اصلی را دوباره به کاربر نمایش می‌دهد (با ویرایش پیام قبلی).
        /// </summary>
        private async Task ShowMainMenuAsync(long chatId, int messageIdToEdit, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Showing main menu again for ChatID {ChatId}", chatId);
                // Use the static GetMainMenuMarkup method from MenuCommandHandler
                (string text, InlineKeyboardMarkup inlineKeyboard) = MenuCommandHandler.GetMainMenuMarkup();
                await EditMessageOrSendNewAsync(chatId, messageIdToEdit, text, inlineKeyboard, cancellationToken: cancellationToken);
            }
            catch (Exception sendEx)
            {
                // If even the fallback fails, log it critically.
                _logger.LogCritical(sendEx, "FALLBACK FAILED: Could not send new message to ChatID {ChatId} after an edit error.", chatId);
            }
        }


        #region Helper Methods
        private async Task AnswerCallbackQuerySilentAsync(string callbackQueryId, CancellationToken cancellationToken, string? text = null, bool showAlert = false)
        {
            try
            {
                await _botClient.AnswerCallbackQuery( // ✅ نام متد صحیح
                    callbackQueryId: callbackQueryId,
                    text: text,
                    showAlert: showAlert,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to answer callback query {CallbackQueryId}. This might happen if the query is too old or already answered.", callbackQueryId);
            }
        }

        private async Task EditMessageOrSendNewAsync(long chatId, int messageId, string text, ReplyMarkup? replyMarkup, ParseMode? parseMode = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _ = await _botClient.EditMessageText( // 
                    chatId: chatId,
                    messageId: messageId,
                    text: text,
                    parseMode: ParseMode.Markdown, // 
                    replyMarkup: (InlineKeyboardMarkup?)replyMarkup,
                    cancellationToken: cancellationToken
                );
            }
            catch (ApiRequestException ex) when (ex.Message.Contains("message to edit not found", StringComparison.OrdinalIgnoreCase) ||
                                                 ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase) ||
                                                 ex.ErrorCode == 400 /* Bad Request, e.g. query is too old */)
            {
                _logger.LogWarning(ex, "Could not edit message (MessageId: {MessageId}, ChatID: {ChatId}) - it might be too old, not found, or not modified. Sending a new message instead.", messageId, chatId);
                await _messageSender.SendTextMessageAsync(chatId, text, parseMode, replyMarkup, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error editing message (MessageId: {MessageId}, ChatID: {ChatId}). Sending a new message instead.", messageId, chatId);
                await _messageSender.SendTextMessageAsync(chatId, text, parseMode, replyMarkup, cancellationToken);
            }
        }
        #endregion
    }
}