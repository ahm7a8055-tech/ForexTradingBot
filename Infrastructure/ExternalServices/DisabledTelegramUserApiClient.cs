using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TL;
using WTelegram;

namespace Infrastructure.Services
{
    /// <summary>
    /// A robust "Null Object" implementation of ITelegramUserApiClient.
    /// This ensures that if the feature is disabled, calls to this service
    /// return safe, empty objects instead of nulls, preventing NullReferenceExceptions
    /// in the calling logic.
    /// </summary>
    public class DisabledTelegramUserApiClient : ITelegramUserApiClient
    {
        private readonly ILogger<DisabledTelegramUserApiClient> _logger;

        // Thread-safe no-op event
        public event Action<Update> OnCustomUpdateReceived { add { } remove { } }

        public bool IsConnected => false;

        // Explicitly returning null here is safe as long as callers check it, 
        // but usually, NativeClient access is rare in business logic.
        public Client NativeClient => null;

        public DisabledTelegramUserApiClient(ILogger<DisabledTelegramUserApiClient> logger)
        {
            _logger = logger;
            // Log at Information level so it's visible on startup
            _logger.LogInformation("⚠️ Auto-Forwarding Module is DISABLED. Using DisabledTelegramUserApiClient.");
        }

        public Task ConnectAndLoginAsync(CancellationToken cancellationToken)
        {
            // Respect cancellation token even in no-op
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);

            _logger.LogWarning("ConnectAndLoginAsync called, but feature is disabled. No connection established.");
            return Task.CompletedTask;
        }

        public Task<Messages_MessagesBase> GetMessagesAsync(InputPeer peer, int[] msgIds, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<Messages_MessagesBase>(cancellationToken);

            _logger.LogWarning("GetMessagesAsync called, but feature is disabled. Returning empty message list.");

            // STRONG FIX: Return an empty object instead of null. 
            // This prevents "Object reference not set to an instance of an object" in the caller.
            Messages_MessagesBase emptyResult = new Messages_Messages
            {
                messages = [],
                chats = [],
                users = []
            };

            return Task.FromResult(emptyResult);
        }

        public Task<UpdatesBase> SendMessageAsync(InputPeer peer, string message, CancellationToken cancellationToken, int? replyToMsgId = null, ReplyMarkup? replyMarkup = null, IEnumerable<MessageEntity>? entities = null, bool noWebpage = false, bool background = false, bool clearDraft = false, DateTime? schedule_date = null, InputMedia? media = null)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<UpdatesBase>(cancellationToken);

            _logger.LogWarning("SendMessageAsync called, but feature is disabled. Returning empty update container.");

            // STRONG FIX: Return an empty Updates object.
            // This mimics a successful API call that resulted in no immediate state changes, preventing crashes.
            UpdatesBase emptyUpdates = new Updates
            {
                updates = [],
                users = [],
                chats = [],
                date = DateTime.UtcNow
            };

            return Task.FromResult(emptyUpdates);
        }

        public Task SendMediaGroupAsync(InputPeer peer, ICollection<InputMedia> media, CancellationToken cancellationToken, string? albumCaption = null, MessageEntity[]? albumEntities = null, int? replyToMsgId = null, bool background = false, DateTime? schedule_date = null, bool sendAsBot = false)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);

            _logger.LogWarning("SendMediaGroupAsync called, but feature is disabled. No action taken.");
            return Task.CompletedTask;
        }

        public Task<UpdatesBase?> ForwardMessagesAsync(InputPeer toPeer, int[] messageIds, InputPeer fromPeer, CancellationToken cancellationToken, bool dropAuthor = false, bool noForwards = false)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<UpdatesBase?>(cancellationToken);

            _logger.LogWarning("ForwardMessagesAsync called, but feature is disabled. Returning null.");

            // For forwarding, returning null is standard behavior for "nothing happened" in many logic flows.
            // However, if your logic demands an object, you could return the 'emptyUpdates' created in SendMessageAsync.
            return Task.FromResult<UpdatesBase?>(null);
        }

        public Task<User?> GetSelfAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("GetSelfAsync called, but feature is disabled. Returning null user.");
            return Task.FromResult<User?>(null);
        }

        public Task<InputPeer?> ResolvePeerAsync(long peerId, CancellationToken cancellationToken)
        {
            _logger.LogWarning("ResolvePeerAsync called for ID {PeerId}, but feature is disabled. Cannot resolve peer.", peerId);
            return Task.FromResult<InputPeer?>(null);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}