using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.DTOs.Responses.Chat;

namespace MyGarage.Interfaces
{
    public interface IChatV2ReceptionMessagingService
    {
        Task<IReadOnlyList<ChatMessageDto>> GetReceptionMessagesAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            long? beforeMessageId,
            int take,
            CancellationToken cancellationToken = default);

        Task<ChatMessageDto> SendStaffMessageAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            ChatSendMessageRequestDto request,
            CancellationToken cancellationToken = default);

        Task MarkStaffReadAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            long? lastReadMessageId,
            CancellationToken cancellationToken = default);
    }
}
