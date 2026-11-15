using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.DTOs.Responses.Chat;

namespace MyGarage.Interfaces
{
    public interface IChatV2CustomerConversationService
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

        Task<ConversationDetailDto> CreateCustomerConversationAsync(
            long customerId,
            ChatConversationCreateRequestDto request,
            CancellationToken cancellationToken = default);

        Task<ConversationDetailDto> GetOrCreateCustomerMessengerConversationAsync(
            long customerId,
            long? preferredBranchId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ChatOrderCardDto>> GetCustomerConversationOrdersAsync(
            long customerId,
            long conversationId,
            CancellationToken cancellationToken = default);
    }
}
