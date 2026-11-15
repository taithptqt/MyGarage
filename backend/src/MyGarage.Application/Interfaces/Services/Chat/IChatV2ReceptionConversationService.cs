using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Chat;

namespace MyGarage.Interfaces
{
    public interface IChatV2ReceptionConversationService
    {
        Task<PagedConversationsResponseDto> GetReceptionConversationsAsync(
            long staffId,
            string? roleCode,
            long? branchId,
            string? status,
            bool? isUnread,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<ConversationDetailDto?> GetReceptionConversationAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ChatOrderCardDto>> GetReceptionConversationOrdersAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            CancellationToken cancellationToken = default);

        Task<ConversationDetailDto> UpdateConversationStatusAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            string status,
            string? reason,
            CancellationToken cancellationToken = default);

        Task<ConversationDetailDto> AssignConversationAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            long assignedStaffId,
            CancellationToken cancellationToken = default);
    }
}
