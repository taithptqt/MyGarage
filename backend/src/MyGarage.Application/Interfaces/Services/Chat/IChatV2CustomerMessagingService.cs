using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.DTOs.Responses.Chat;

namespace MyGarage.Interfaces
{
    public interface IChatV2CustomerMessagingService
    {
        Task<IReadOnlyList<ChatMessageDto>> GetCustomerMessagesAsync(
            long customerId,
            long conversationId,
            long? beforeMessageId,
            int take,
            CancellationToken cancellationToken = default);

        Task<ChatMessageDto> SendCustomerMessageAsync(
            long customerId,
            long conversationId,
            ChatSendMessageRequestDto request,
            CancellationToken cancellationToken = default);

        Task MarkCustomerReadAsync(
            long customerId,
            long conversationId,
            long? lastReadMessageId,
            CancellationToken cancellationToken = default);
    }
}
