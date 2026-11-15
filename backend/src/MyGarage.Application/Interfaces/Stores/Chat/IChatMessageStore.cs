using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Chat;

public interface IChatMessageStore
{
    Task<ChatMessage?> GetChatMessageByClientMessageIdAsync(Guid clientMessageId, CancellationToken ct = default);

    void AddChatMessage(ChatMessage message);

    Task<List<ChatMessage>> GetConversationMessagesAsync(
        long conversationId,
        long? beforeMessageId,
        int take,
        CancellationToken ct = default);

    Task<long?> GetMaxChatMessageIdAsync(long conversationId, CancellationToken ct = default);

    Task<bool> ChatMessageExistsInConversationAsync(
        long messageId,
        long conversationId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ChatMessageAttachment>> GetAttachmentsByMessageIdsAsync(
        IReadOnlyCollection<long> messageIds,
        CancellationToken ct = default);
}
