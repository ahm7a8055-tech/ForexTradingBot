// File: TelegramPanel/Infrastructure/TelegramBotService.cs
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Threading.Channels;
using Telegram.Bot;
using Telegram.Bot.Exceptions;  // ✅ برای ApiRequestException
using Telegram.Bot.Polling; // ✅ برای IUpdateHandler, DefaultUpdateHandlerOptions
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums; // ✅ برای UpdateType
using TelegramPanel.Infrastructure.Services;
using TelegramPanel.Queue;
using TelegramPanel.Settings;
using User = Telegram.Bot.Types.User;

namespace TelegramPanel.Infrastructure
{
    public class TelegramBotService : IHostedService, IUpdateHandler // ✅ پیاده‌سازی IUpdateHandler
    {
        private readonly ILogger<TelegramBotService> _logger;
        private readonly ITelegramBotClient _botClient;
        private readonly TelegramPanelSettings _settings;
        private readonly ITelegramUpdateChannel _updateChannel;
        private CancellationTokenSource? _cancellationTokenSourceForPolling; // جداگانه برای Polling
        private readonly BotCommandSetupService _commandSetupService; // برای تنظیم کامندها
        private readonly ActivitySource _activitySource;
        private readonly IServiceProvider _serviceProvider;
        public TelegramBotService(
            ILogger<TelegramBotService> logger,
            ITelegramBotClient botClient,
            IOptions<TelegramPanelSettings> settingsOptions,
            ITelegramUpdateChannel updateChannel,
            IBotCommandSetupService commandSetupService,
            IServiceProvider serviceProvider) // Inject IServiceProvider
        {
            _activitySource = new ActivitySource("TelegramPanel.Infrastructure");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
            _settings = settingsOptions?.Value ?? throw new ArgumentNullException(nameof(settingsOptions));
            _updateChannel = updateChannel ?? throw new ArgumentNullException(nameof(updateChannel));
            _commandSetupService = (BotCommandSetupService?)commandSetupService ?? throw new ArgumentNullException(nameof(commandSetupService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }


        public async Task StartAsync(CancellationToken hostCancellationToken)
        {
            _cancellationTokenSourceForPolling = CancellationTokenSource.CreateLinkedTokenSource(hostCancellationToken);
            User? me;

            #region Bot Information Retrieval
            try
            {
                _logger.LogInformation("Attempting to connect to Telegram and get bot information...");
                me = await _botClient.GetMe(cancellationToken: _cancellationTokenSourceForPolling.Token);
                _logger.LogInformation("Successfully connected. Bot Service starting for: {BotUsername} (ID: {BotId})", me.Username, me.Id);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Bot service startup was canceled during GetMeAsync.");
                return; // اگر عملیات قبل از اتصال کنسل شد، ادامه ندهید.
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to get bot info (GetMeAsync). Bot token might be invalid, network issues, or Telegram API is down. Bot service will not start.");
                _ = Task.Run(async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IProMonitoringLogRepository>();
                    await repo.AddAsync(new ProMonitoringLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "Critical",
                        Source = "TelegramBotService",
                        EventType = "StartAsync.GetMeAsync",
                        Message = ex.Message,
                        Details = ex.StackTrace,
                        Exception = ex.ToString(),
                        Status = "Failed",
                        CreatedAt = DateTime.UtcNow
                    });
                });
                return; // بدون اطلاعات ربات، ادامه کار ممکن نیست.
            }
            #endregion

            #region Bot Command Setup
            _logger.LogInformation("Setting up bot commands...");
            await _commandSetupService.SetupCommandsAsync(_cancellationTokenSourceForPolling.Token);
            _logger.LogInformation("Bot commands setup complete.");
            #endregion

            // Ensure cancellation is linked to the host's token for proper shutdown
            _cancellationTokenSourceForPolling = CancellationTokenSource.CreateLinkedTokenSource(hostCancellationToken);

            bool useWebhookMode = _settings.UseWebhook && !string.IsNullOrWhiteSpace(_settings.WebhookAddress);
            bool webhookSuccessfullySet = false;

            // Always try to delete webhook first to ensure clean state
            await TryDeleteWebhookAsync(_cancellationTokenSourceForPolling.Token, "Ensuring clean state before starting bot service.");

            if (useWebhookMode)
            {
                #region Webhook Setup Attempt
                try
                {
                    _logger.LogInformation("Webhook usage is enabled in settings. Attempting to configure Webhook to address: {WebhookAddress}", _settings.WebhookAddress);
                    // ابتدا هرگونه Webhook قبلی را حذف می‌کنیم تا از تداخل جلوگیری شود.
                    await TryDeleteWebhookAsync(_cancellationTokenSourceForPolling.Token, "Preparing for new Webhook setup.");

                    UpdateType[] allowedUpdatesForWebhook = _settings.AllowedUpdates?.ToArray() ?? Array.Empty<UpdateType>();

                    try
                    {
                        // Add this logging before the _botClient.SetWebhook call
                        _logger.LogInformation("Webhook setup details: useWebhookMode={UseWebhookMode}, UseWebhookSetting={UseWebhookSetting}, WebhookAddress={WebhookAddress}, WebhookSecretToken={WebhookSecretToken}",
                                                   useWebhookMode, _settings.UseWebhook, _settings.WebhookAddress, _settings.WebhookSecretToken ?? "Not Set");

                        await _botClient.SetWebhook(
                            url: _settings.WebhookAddress!, // Non-null due to IsNullOrWhiteSpace check
                            allowedUpdates: allowedUpdatesForWebhook,
                            dropPendingUpdates: _settings.DropPendingUpdatesOnWebhookSet,
                            secretToken: _settings.WebhookSecretToken,
                            cancellationToken: _cancellationTokenSourceForPolling.Token);

                        WebhookInfo? webhookInfo = await _botClient.GetWebhookInfo(cancellationToken: _cancellationTokenSourceForPolling.Token);
                        if (webhookInfo != null && webhookInfo.Url.Equals(_settings.WebhookAddress, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Webhook configured successfully to: {WebhookAddress}. Pending updates: {PendingUpdates}. Last error: {LastErrorMsg} at {LastErrorDate}",
                                webhookInfo.Url, webhookInfo.PendingUpdateCount, webhookInfo.LastErrorMessage ?? "None", webhookInfo.LastErrorDate?.ToLocalTime().ToString() ?? "N/A");
                            //    webhookSuccessfullySet = true;
                        }
                        else
                        {
                            _logger.LogWarning("Webhook URL was set, but GetWebhookInfo verification failed. Actual URL: '{ActualUrl}', Configured: '{ConfiguredUrl}', Last Error: '{LastError}'. Will fall back to polling.",
                                webhookInfo?.Url ?? "Not Set", _settings.WebhookAddress, webhookInfo?.LastErrorMessage ?? "N/A");
                            // تلاش برای حذف Webhook ناموفق، چون می‌خواهیم به Polling برویم.
                            await TryDeleteWebhookAsync(_cancellationTokenSourceForPolling.Token, "Webhook verification failed after setting, preparing for polling.");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Webhook setup was canceled during SetWebhook or GetWebhookInfo. This might happen during application shutdown. Falling back to polling if not already stopping.");
                        // No need to rethrow, just let the code proceed to the polling section if not stopping.
                    }
                }
                catch (Exception ex) // شامل ApiRequestException
                {
                    _logger.LogError(ex, "Failed to set or verify webhook at {WebhookAddress}. Error: {ErrorMessage}. Will fall back to polling.",
                        _settings.WebhookAddress, ex.Message);
                    _ = Task.Run(async () =>
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IProMonitoringLogRepository>();
                        await repo.AddAsync(new ProMonitoringLog
                        {
                            Timestamp = DateTime.UtcNow,
                            Level = "Error",
                            Source = "TelegramBotService",
                            EventType = "StartAsync.WebhookSetup",
                            Message = ex.Message,
                            Details = ex.StackTrace,
                            Exception = ex.ToString(),
                            Status = "Failed",
                            CreatedAt = DateTime.UtcNow
                        });
                    });
                    // Attempt to delete Webhook if its setup failed.
                    await TryDeleteWebhookAsync(_cancellationTokenSourceForPolling.Token, "Webhook setup failed, preparing for polling.");
                }
                #endregion
            }
            else // UseWebhook is false or WebhookAddress is not configured
            {
                _logger.LogInformation("Webhook usage is disabled or WebhookAddress is not configured in settings. Bot will use polling.");
                // No need to delete webhook here as we already tried above
            }

            // اگر تنظیم Webhook ناموفق بود یا از ابتدا برای Polling پیکربندی شده بود
            if (!webhookSuccessfullySet)
            {
                #region Polling Setup
                try
                {
                    // Double check webhook is deleted before starting polling
                    WebhookInfo webhookInfo = await _botClient.GetWebhookInfo(cancellationToken: _cancellationTokenSourceForPolling.Token);
                    if (!string.IsNullOrEmpty(webhookInfo.Url))
                    {
                        _logger.LogWarning("Webhook still active at {WebhookUrl}. Attempting to delete before starting polling.", webhookInfo.Url);
                        await _botClient.DeleteWebhook(dropPendingUpdates: true, cancellationToken: _cancellationTokenSourceForPolling.Token);
                        _logger.LogInformation("Webhook deleted successfully before starting polling.");
                    }

                    UpdateType[] allowedUpdatesForPolling = _settings.AllowedUpdates?.ToArray() ?? Array.Empty<UpdateType>();
                    ReceiverOptions receiverOptions = new()
                    {
                        AllowedUpdates = allowedUpdatesForPolling,
                        //  اگر نیاز به مدیریت offset دارید، این بخش باید با دقت بیشتری بررسی شود.
                        //  کتابخانه ممکن است به طور خودکار آخرین آپدیت‌ها را دریافت کند.
                    };

                    _botClient.StartReceiving(
                        updateHandler: this, // این کلاس IUpdateHandler را پیاده‌سازی می‌کند
                        receiverOptions: receiverOptions,
                        cancellationToken: _cancellationTokenSourceForPolling.Token // استفاده از CancellationToken داخلی
                    );
                    _logger.LogInformation("Polling started successfully for bot: {BotUsername}.", me.Username);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "CRITICAL: Failed to start polling for bot {BotUsername}. The bot may not receive updates via polling.", me.Username);
                    _ = Task.Run(async () =>
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IProMonitoringLogRepository>();
                        await repo.AddAsync(new ProMonitoringLog
                        {
                            Timestamp = DateTime.UtcNow,
                            Level = "Critical",
                            Source = "TelegramBotService",
                            EventType = "StartAsync.PollingSetup",
                            Message = ex.Message,
                            Details = ex.StackTrace,
                            Exception = ex.ToString(),
                            Status = "Failed",
                            CreatedAt = DateTime.UtcNow
                        });
                    });
                    // در این حالت، اگر Webhook هم تنظیم نشده باشد، ربات کار نخواهد کرد.
                }
                #endregion
            }
            else // Webhook با موفقیت تنظیم شده است
            {
                _logger.LogInformation("Webhook is active. Polling will not be started.");
            }
        }


        /// <summary>
        /// تلاش می‌کند Webhook فعلی را حذف کند و نتیجه را لاگ می‌کند.
        /// </summary>
        /// <summary>
        /// تلاش می‌کند Webhook فعلی را حذف کند و نتیجه را لاگ می‌کند.
        /// </summary>
        private async Task TryDeleteWebhookAsync(CancellationToken cancellationToken, string reasonForDeletion)
        {
            _logger.LogInformation("Attempting to delete existing webhook. Reason: {Reason}", reasonForDeletion);
            try
            {
                // بررسی اینکه آیا Webhook ای اصلاً تنظیم شده است
                WebhookInfo currentWebhookInfo = await _botClient.GetWebhookInfo(cancellationToken);
                if (!string.IsNullOrEmpty(currentWebhookInfo.Url))
                {
                    await _botClient.DeleteWebhook(dropPendingUpdates: true, cancellationToken: cancellationToken);
                    _logger.LogInformation("Webhook previously set to '{PreviousUrl}' was deleted successfully.", currentWebhookInfo.Url);
                }
                else
                {
                    _logger.LogInformation("No active webhook was set, so no deletion was necessary.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete webhook (or verify its absence). This might be an issue if a webhook was previously set and now switching to polling.");
            }
        }


        #region IUpdateHandler Implementation (for Polling)
        /// <summary>
        /// This method is invoked by the Telegram.Bot library's Polling mechanism for every new update.
        /// Its responsibility is to safely and efficiently send the update to the internal processing channel (<see cref="ITelegramUpdateChannel"/>).
        /// This enhanced version prioritizes robustness, detailed logging, and graceful handling of various scenarios.
        /// </summary>
        /// <param name="botClient">The bot client that received the update (typically the same as <see cref="_botClient"/>).</param>
        /// <param name="update">The update object received from Telegram.</param>
        /// <param name="cancellationToken">A token passed by the Polling loop, indicating a request to stop polling.</param>
        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // --- 1. Input Validation ---
            if (botClient == null)
            {
                // Critical configuration error, should halt startup or be caught higher up.
                throw new ArgumentNullException(nameof(botClient), "The bot client cannot be null when handling an update.");
            }

            if (update == null)
            {
                // Log warning and exit gracefully. Null updates might occur in rare edge cases.
                _logger.LogWarning("Polling: Received a null update object. Skipping processing.");
                // Note: No tracing span is created here as there's no meaningful update context.
                return;
            }

            // --- 2. Distributed Tracing Setup ---
            // Use a descriptive name for the activity. ActivityKind.Internal is suitable for internal processing.
            using Activity? activity = _activitySource.CreateActivity("HandleUpdateAsync", ActivityKind.Internal);

            // Add common tags for tracing context. These tags are visible in distributed tracing systems (like Jaeger, Zipkin).
            _ = (activity?.AddTag("app.update.id", update.Id));
            _ = (activity?.AddTag("app.update.type", update.Type.ToString()));

            // Extract user ID and add it as a tag if available.
            long? userId = update.Message?.From?.Id ?? update.CallbackQuery?.From?.Id;
            if (userId.HasValue)
            {
                _ = (activity?.AddTag("app.telegram.user.id", userId.Value));
            }

            // Extract chat ID and add it as a tag if available.
            long? chatId = update.Message?.Chat?.Id ?? update.CallbackQuery?.Message?.Chat?.Id;
            if (chatId.HasValue)
            {
                _ = (activity?.AddTag("app.telegram.chat.id", chatId.Value));
            }

            // Log a snippet of message text for context, but only if it's a message update.
            string? messageTextSnippet = null;
            if (update.Message?.Text != null)
            {
                messageTextSnippet = update.Message.Text.Length > 50
                    ? update.Message.Text[..50] + "..."
                    : update.Message.Text;
                _ = (activity?.AddTag("app.message.text.snippet", messageTextSnippet));
            }

            // Start the activity (trace span).
            _ = (activity?.Start());

            // --- 3. Structured Logging Context ---
            // Combine tracing tags with logging scope properties for comprehensive context.
            Dictionary<string, object?> logScopeProps = new()
            {
                ["Source"] = nameof(HandleUpdateAsync),
                ["UpdateId"] = update.Id,
                ["UpdateType"] = update.Type.ToString(),
                ["TelegramUserId"] = userId,
                ["TelegramChatId"] = chatId,
                ["MessageTextSnippet"] = messageTextSnippet,
                // Add a reference to the current trace/span ID for easier log correlation
                ["TraceId"] = activity?.TraceId.ToString(),
                ["SpanId"] = activity?.SpanId.ToString()
            };

            using (_logger.BeginScope(logScopeProps))
            {
                _logger.LogDebug("Received update. Attempting to enqueue for processing.");

                // --- 4. Channel Write Operation with Enhanced Error Handling ---
                try
                {
                    // Attempt to write the update to the channel.
                    // cancellationToken ensures this operation respects the polling cancellation request.
                    await _updateChannel.WriteAsync(update, cancellationToken).ConfigureAwait(false);

                    _logger.LogTrace("Update successfully enqueued to the processing channel.");
                    // Potential Metric: Increment a counter for successful enqueues
                    // _metrics.EnqueueSuccessCount.Inc();
                }
                catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected during graceful shutdown. Log as Information.
                    _logger.LogInformation(oce, "Enqueueing update was canceled due to polling cancellation request.");
                    // Potential Metric: Increment a counter for canceled operations
                    // _metrics.EnqueueCanceledCount.Inc();
                }
                catch (ChannelClosedException cce)
                {
                    // Critical error: Channel is closed, cannot enqueue. Application shutdown likely.
                    _logger.LogError(cce, "Failed to enqueue update to the processing channel because the channel is closed. Application might be shutting down or channel terminated unexpectedly.");
                    // Potential Metric: Increment a counter for channel closed errors
                    // _metrics.EnqueueChannelClosedErrorCount.Inc();

                    // Mark the activity as failed if the channel write fails critically.
                    _ = (activity?.SetStatus(ActivityStatusCode.Error, "Channel closed during enqueue"));
                }
                catch (Exception ex)
                {
                    // Catch-all for any other unexpected errors during the enqueue operation.
                    _logger.LogError(ex, "An unexpected error occurred while enqueueing update from polling to the processing channel.");
                    // Potential Metric: Increment a counter for general enqueue errors
                    // _metrics.EnqueueGenericErrorCount.Inc();

                    // Mark the activity as failed.
                    _ = (activity?.SetStatus(ActivityStatusCode.Error, $"Enqueue failed: {ex.GetType().Name}"));
                }
                finally
                {
                    // Ensure the activity is always stopped, regardless of success or failure.
                    activity?.Stop();
                }
            }
        }


        #endregion


        /// <summary>
        /// این متد توسط مکانیزم Polling کتابخانه Telegram.Bot هنگام بروز خطا در فرآیند Polling فراخوانی می‌شود.
        /// </summary>
        /// <param name="botClient">کلاینت ربات.</param>
        /// <param name="exception">Exception رخ داده.</param>
        /// <param name="source">منبع خطا در حلقه Polling (مثلاً از GetUpdates, HandleUpdate, یا HandleError).</param> // ✅ پارامتر جدید
        /// <param name="cancellationToken">توکن کنسل شدن Polling.</param>
        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
        {
            // This call is correct for modern .NET/DiagnosticSource versions.
            using Activity? activity = _activitySource.CreateActivity(name: "TelegramBot.HandleError", kind: ActivityKind.Internal);
            _ = (activity?.Start());

            try
            {
                string formattedErrorMessage;
                string logMessage = "An error occurred during Telegram Bot polling.";
                ActivityStatusCode? activityStatus = null;
                string? activityStatusDescription = null;

                if (exception is ApiRequestException apiEx)
                {
                    switch (apiEx.ErrorCode)
                    {
                        case 401:
                            formattedErrorMessage = $"Telegram API Error (Unauthorized - 401, Source: {source}): Bot token invalid or revoked. Message='{apiEx.Message}'.";
                            logMessage = $"CRITICAL POLLING ERROR: {formattedErrorMessage}";
                            activityStatus = ActivityStatusCode.Error;
                            activityStatusDescription = "Unauthorized (401)";
                            break;
                        case 403:
                            formattedErrorMessage = $"Telegram API Warning (Forbidden - 403, Source: {source}): Bot blocked or lacks permissions. Message='{apiEx.Message}'.";
                            logMessage = $"POLLING INFO: {formattedErrorMessage}";
                            // FIX for CS1503: The ActivityEvent constructor takes (name, timestamp, tags). We omit the timestamp to use UtcNow.
                            _ = (activity?.AddEvent(new ActivityEvent("TelegramApiWarning", tags: new ActivityTagsCollection {
                                { "error.code", 403 }, { "error.message", apiEx.Message }
                            })));
                            break;
                        case 429:
                            formattedErrorMessage = $"Telegram API Warning (Too Many Requests - 429, Source: {source}): Bot hitting rate limits. Message='{apiEx.Message}'.";
                            logMessage = $"POLLING WARNING: {formattedErrorMessage}";
                            // FIX for CS1503: Correct constructor usage.
                            _ = (activity?.AddEvent(new ActivityEvent("TelegramApiRateLimit", tags: new ActivityTagsCollection {
                                { "error.code", 429 }, { "error.message", apiEx.Message }
                            })));
                            break;
                        default:
                            formattedErrorMessage = $"Telegram API Error (Code: {apiEx.ErrorCode}, Source: {source}): Message='{apiEx.Message}'.";
                            logMessage = $"Telegram API Error encountered: {formattedErrorMessage}";
                            activityStatus = ActivityStatusCode.Error;
                            activityStatusDescription = $"API Error {apiEx.ErrorCode}";
                            break;
                    }
                }
                else
                {
                    formattedErrorMessage = $"Polling Exception (Source: {source}): Type='{exception.GetType().FullName}', Message='{exception.Message}'";
                    logMessage = $"An unexpected polling exception occurred: {formattedErrorMessage}";
                    activityStatus = ActivityStatusCode.Error;
                    activityStatusDescription = $"Polling Exception: {exception.GetType().Name}";
                }

                Dictionary<string, object?> logScopeProps = new()
                {
                    ["ErrorSource"] = source.ToString(),
                    ["ExceptionType"] = exception.GetType().Name,
                    ["TraceId"] = activity?.TraceId.ToString(),
                    ["SpanId"] = activity?.SpanId.ToString()
                };

                using (_logger.BeginScope(logScopeProps))
                {
                    _logger.LogError(exception, "{LogMessage}", logMessage);
                    _ = Task.Run(async () =>
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IProMonitoringLogRepository>();
                        await repo.AddAsync(new ProMonitoringLog
                        {
                            Timestamp = DateTime.UtcNow,
                            Level = "Error",
                            Source = "TelegramBotService",
                            EventType = "HandleErrorAsync",
                            Message = exception.Message,
                            Details = exception.StackTrace,
                            Exception = exception.ToString(),
                            Status = "Failed",
                            CreatedAt = DateTime.UtcNow
                        });
                    });
                }

                if (exception is ApiRequestException apiExForPollingStop && apiExForPollingStop.ErrorCode == 401)
                {
                    _logger.LogCritical("CRITICAL: Unauthorized (401) error detected. Stopping polling to prevent further issues.");
                    _ = Task.Run(async () =>
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IProMonitoringLogRepository>();
                        await repo.AddAsync(new ProMonitoringLog
                        {
                            Timestamp = DateTime.UtcNow,
                            Level = "Critical",
                            Source = "TelegramBotService",
                            EventType = "HandleErrorAsync.401",
                            Message = "Unauthorized (401) error detected. Stopping polling to prevent further issues.",
                            Details = null,
                            Exception = null,
                            Status = "Failed",
                            CreatedAt = DateTime.UtcNow
                        });
                    });
                    _cancellationTokenSourceForPolling?.Cancel();
                }

                if (activityStatus.HasValue)
                {
                    _ = (activity?.SetStatus(activityStatus.Value, activityStatusDescription));
                }
                else if (activity?.Status == ActivityStatusCode.Unset && exception is ApiRequestException apiExForStatus && !(apiExForStatus.ErrorCode == 403 || apiExForStatus.ErrorCode == 429))
                {
                    _ = (activity?.SetStatus(ActivityStatusCode.Error, $"Unhandled API error: {apiExForStatus.ErrorCode}"));
                }
            }
            catch (Exception handlerEx)
            {
                Console.Error.WriteLine($"FATAL ERROR IN HandleErrorAsync: Handler failed. Original: {exception?.GetType().Name ?? "Unknown"}, Handler: {handlerEx.GetType().Name}.");
                _ = (activity?.SetStatus(ActivityStatusCode.Error, $"Handler failed: {handlerEx.GetType().Name}"));
                _ = Task.Run(async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IProMonitoringLogRepository>();
                    await repo.AddAsync(new ProMonitoringLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "Critical",
                        Source = "TelegramBotService",
                        EventType = "HandleErrorAsync.HandlerFailed",
                        Message = handlerEx.Message,
                        Details = handlerEx.StackTrace,
                        Exception = handlerEx.ToString(),
                        Status = "Failed",
                        CreatedAt = DateTime.UtcNow
                    });
                });
            }
            finally
            {
                activity?.Stop();
            }

            return Task.CompletedTask;
        }



        /// <summary>
        /// This method is called when the host requests the service to stop.
        /// It's responsible for initiating a graceful shutdown of the Telegram bot,
        /// including stopping polling and cleaning up the webhook if necessary.
        /// </summary>
        /// <param name="hostCancellationToken">A token indicating that the host is shutting down.</param>
        public async Task StopAsync(CancellationToken hostCancellationToken)
        {
            _logger.LogInformation("Bot Service StopAsync called. Initiating shutdown procedures.");

            // --- 1. Webhook Cleanup ---
            // If webhook mode is enabled, attempt to delete the webhook to prevent Telegram
            // from sending updates to a defunct address.
            if (_settings.UseWebhook && !string.IsNullOrWhiteSpace(_settings.WebhookAddress))
            {
                _logger.LogInformation("Webhook mode is active. Attempting to delete webhook for cleanup.");
                // Use a dedicated CancellationTokenSource with a timeout for webhook cleanup.
                // This prevents a stuck webhook deletion from blocking the entire shutdown process.
                using CancellationTokenSource cleanupCts = new(TimeSpan.FromSeconds(15)); // Increased timeout to 15 seconds for webhook operations
                CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(hostCancellationToken, cleanupCts.Token);

                try
                {
                    await TryDeleteWebhookAsync(linkedCts.Token, "Application shutting down.");
                    _logger.LogInformation("Webhook cleanup process completed.");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Webhook cleanup operation was canceled (host shutdown or cleanup timeout).");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during webhook cleanup while stopping the service.");
                    // We don't want cleanup errors to prevent other shutdown steps.
                }
                finally
                {
                    // Ensure the linked CTS is disposed, which also disposes the individual CTSs.
                    linkedCts.Dispose();
                }
            }
            else
            {
                _logger.LogInformation("Webhook mode is not active or address is not configured. Skipping webhook cleanup.");
            }

            // --- 2. Polling Shutdown ---
            // Cancel the internal CancellationTokenSource that controls the polling loop.
            if (_cancellationTokenSourceForPolling != null && !_cancellationTokenSourceForPolling.IsCancellationRequested)
            {
                _logger.LogInformation("Requesting cancellation of internal operations (e.g., polling loop).");
                try
                {
                    _cancellationTokenSourceForPolling.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogWarning("The internal CancellationTokenSource was already disposed. No action taken.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while attempting to cancel the internal CancellationTokenSource.");
                }
            }
            else
            {
                _logger.LogInformation("Internal CancellationTokenSource is null or already canceled. No action needed for polling shutdown.");
            }

            // --- 3. Resource Management ---
            // Dispose the CancellationTokenSource to release its resources.
            // It's good practice to do this after signaling cancellation.
            if (_cancellationTokenSourceForPolling != null)
            {
                try
                {
                    _cancellationTokenSourceForPolling.Dispose();
                    _logger.LogInformation("Internal CancellationTokenSource disposed.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while disposing the internal CancellationTokenSource.");
                }
                // Set to null to indicate it's no longer valid.
                _cancellationTokenSourceForPolling = null;
            }

            _logger.LogInformation("Bot Service has completed its stopping procedures.");
        }
    }
}