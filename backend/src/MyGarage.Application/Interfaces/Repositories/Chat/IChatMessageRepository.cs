using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IChatMessageRepository : IGenericRepository<ChatMessage>
    {
        Task<IReadOnlyList<ChatMessage>> GetConversationMessagesAsync(
            long conversationId,
            long? beforeMessageId,
            int take,
            CancellationToken cancellationToken = default);

        Task<ChatMessage?> GetByClientMessageIdAsync(Guid clientMessageId, CancellationToken cancellationToken = default);
    }
}
