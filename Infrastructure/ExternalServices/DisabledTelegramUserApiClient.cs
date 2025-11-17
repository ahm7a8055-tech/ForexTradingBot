using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TL;
using WTelegram;

namespace Infrastructure.Services
{
    /// <summary>
    /// A "Null Object" implementation of the ITelegramUserApiClient.
    /// This is registered with the DI container when the auto-forwarding feature is disabled.
    /// It allows other services to safely inject the interface without causing startup errors,
    /// and its methods do nothing, effectively disabling the feature.
    /// </summary>
    public class DisabledTelegramUserApiClient : ITelegramUserApiClient
    {
        private readonly ILogger<DisabledTelegramUserApiClient> _logger;

        public event Action<Update> OnCustomUpdateReceived;

        public bool IsConnected => false;

        public Client NativeClient => throw new NotImplementedException();

        public DisabledTelegramUserApiClient(ILogger<DisabledTelegramUserApiClient> logger)
        {
            _logger = logger;
            _logger.LogInformation("Auto-Forwarding is disabled. The DisabledTelegramUserApiClient is active.");
        }

        public Task ConnectAndLoginAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("ConnectAndLoginAsync was called, but the feature is disabled. No action taken.");
            return Task.CompletedTask;
        }

        // --- Implement all other public methods from ITelegramUserApiClient ---
        // For each method, just log that it was called and do nothing.
        // For methods that return a value, return a sensible default (null, false, empty list, etc.).

        // Example for a method that forwards a message:
        public Task ForwardMessageAsync(long targetChatId, int messageId, long fromChatId)
        {
            _logger.LogDebug("ForwardMessageAsync was called, but the feature is disabled.");
            return Task.CompletedTask;
        }

        // Example for a method that returns a value:
        public Task<string> GetSomeDataAsync()
        {
            _logger.LogDebug("GetSomeDataAsync was called, but the feature is disabled.");
            return Task.FromResult<string>(null); // or Task.FromResult(string.Empty)
        }

        public Task<Messages_MessagesBase> GetMessagesAsync(InputPeer peer, int[] msgIds, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<UpdatesBase> SendMessageAsync(InputPeer peer, string message, CancellationToken cancellationToken, int? replyToMsgId = null, ReplyMarkup? replyMarkup = null, IEnumerable<MessageEntity>? entities = null, bool noWebpage = false, bool background = false, bool clearDraft = false, DateTime? schedule_date = null, InputMedia? media = null)
        {
            throw new NotImplementedException();
        }

        public Task SendMediaGroupAsync(InputPeer peer, ICollection<InputMedia> media, CancellationToken cancellationToken, string? albumCaption = null, MessageEntity[]? albumEntities = null, int? replyToMsgId = null, bool background = false, DateTime? schedule_date = null, bool sendAsBot = false)
        {
            throw new NotImplementedException();
        }

        public Task<UpdatesBase?> ForwardMessagesAsync(InputPeer toPeer, int[] messageIds, InputPeer fromPeer, CancellationToken cancellationToken, bool dropAuthor = false, bool noForwards = false)
        {
            throw new NotImplementedException();
        }

        public Task<User?> GetSelfAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<InputPeer?> ResolvePeerAsync(long peerId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        // Add all other methods from your ITelegramUserApiClient interface here...
    }
}