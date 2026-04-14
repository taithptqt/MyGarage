using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.DTOs.Responses.Chat;

namespace MyGarage.Interfaces
{
    public interface IChatV2Service
    {
        Task<PagedConversationsResponseDto> GetCustomerConversationsAsync(
            long customerId,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<ConversationDetailDto?> GetCustomerConversationAsync(
            long customerId,
            long conversationId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ChatMessageDto>> GetCustomerMessagesAsync(
            long customerId,
            long conversationId,
            long? beforeMessageId,
            int take,
            CancellationToken cancellationToken = default);

        Task<ConversationDetailDto> CreateCustomerConversationAsync(
            long customerId,
            ChatConversationCreateRequestDto request,
            CancellationToken cancellationToken = default);

        Task<ConversationDetailDto> GetOrCreateCustomerMessengerConversationAsync(
            long customerId,
            long? preferredBranchId,
            CancellationToken cancellationToken = default);

        Task<ChatMessageDto> SendCustomerMessageAsync(
            long customerId,
            long conversationId,
            ChatSendMessageRequestDto request,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ChatOrderCardDto>> GetCustomerConversationOrdersAsync(
            long customerId,
            long conversationId,
            CancellationToken cancellationToken = default);

        Task MarkCustomerReadAsync(
            long customerId,
            long conversationId,
            long? lastReadMessageId,
            CancellationToken cancellationToken = default);

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

        Task MarkStaffReadAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            long? lastReadMessageId,
            CancellationToken cancellationToken = default);

        Task<ConversationDetailDto> AssignConversationAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            long assignedStaffId,
            CancellationToken cancellationToken = default);
    }
}
