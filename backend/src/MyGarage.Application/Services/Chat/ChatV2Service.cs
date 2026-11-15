using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.DTOs.Responses.Chat;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ChatV2Service : IChatV2Service
    {
        private readonly IChatV2CustomerWorkflowService _customerWorkflow;
        private readonly IChatV2ReceptionWorkflowService _receptionWorkflow;

        public ChatV2Service(
            IChatV2CustomerWorkflowService customerWorkflow,
            IChatV2ReceptionWorkflowService receptionWorkflow)
        {
            _customerWorkflow = customerWorkflow;
            _receptionWorkflow = receptionWorkflow;
        }

        public Task<PagedConversationsResponseDto> GetCustomerConversationsAsync(
            long customerId,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return _customerWorkflow.GetCustomerConversationsAsync(customerId, status, page, pageSize, cancellationToken);
        }

        public Task<ConversationDetailDto?> GetCustomerConversationAsync(
            long customerId,
            long conversationId,
            CancellationToken cancellationToken = default)
        {
            return _customerWorkflow.GetCustomerConversationAsync(customerId, conversationId, cancellationToken);
        }

        public Task<ConversationDetailDto> CreateCustomerConversationAsync(
            long customerId,
            ChatConversationCreateRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return _customerWorkflow.CreateCustomerConversationAsync(customerId, request, cancellationToken);
        }

        public Task<ConversationDetailDto> GetOrCreateCustomerMessengerConversationAsync(
            long customerId,
            long? preferredBranchId,
            CancellationToken cancellationToken = default)
        {
            return _customerWorkflow.GetOrCreateCustomerMessengerConversationAsync(customerId, preferredBranchId, cancellationToken);
        }

        public Task<IReadOnlyList<ChatOrderCardDto>> GetCustomerConversationOrdersAsync(
            long customerId,
            long conversationId,
            CancellationToken cancellationToken = default)
        {
            return _customerWorkflow.GetCustomerConversationOrdersAsync(customerId, conversationId, cancellationToken);
        }

        public Task<IReadOnlyList<ChatMessageDto>> GetCustomerMessagesAsync(
            long customerId,
            long conversationId,
            long? beforeMessageId,
            int take,
            CancellationToken cancellationToken = default)
        {
            return _customerWorkflow.GetCustomerMessagesAsync(customerId, conversationId, beforeMessageId, take, cancellationToken);
        }

        public Task<ChatMessageDto> SendCustomerMessageAsync(
            long customerId,
            long conversationId,
            ChatSendMessageRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return _customerWorkflow.SendCustomerMessageAsync(customerId, conversationId, request, cancellationToken);
        }

        public Task MarkCustomerReadAsync(
            long customerId,
            long conversationId,
            long? lastReadMessageId,
            CancellationToken cancellationToken = default)
        {
            return _customerWorkflow.MarkCustomerReadAsync(customerId, conversationId, lastReadMessageId, cancellationToken);
        }

        public Task<PagedConversationsResponseDto> GetReceptionConversationsAsync(
            long staffId,
            string? roleCode,
            long? branchId,
            string? status,
            bool? isUnread,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return _receptionWorkflow.GetReceptionConversationsAsync(staffId, roleCode, branchId, status, isUnread, page, pageSize, cancellationToken);
        }

        public Task<ConversationDetailDto?> GetReceptionConversationAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            CancellationToken cancellationToken = default)
        {
            return _receptionWorkflow.GetReceptionConversationAsync(staffId, roleCode, conversationId, cancellationToken);
        }

        public Task<IReadOnlyList<ChatOrderCardDto>> GetReceptionConversationOrdersAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            CancellationToken cancellationToken = default)
        {
            return _receptionWorkflow.GetReceptionConversationOrdersAsync(staffId, roleCode, conversationId, cancellationToken);
        }

        public Task<ConversationDetailDto> UpdateConversationStatusAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            string status,
            string? reason,
            CancellationToken cancellationToken = default)
        {
            return _receptionWorkflow.UpdateConversationStatusAsync(staffId, roleCode, conversationId, status, reason, cancellationToken);
        }

        public Task<ConversationDetailDto> AssignConversationAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            long assignedStaffId,
            CancellationToken cancellationToken = default)
        {
            return _receptionWorkflow.AssignConversationAsync(staffId, roleCode, conversationId, assignedStaffId, cancellationToken);
        }

        public Task<IReadOnlyList<ChatMessageDto>> GetReceptionMessagesAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            long? beforeMessageId,
            int take,
            CancellationToken cancellationToken = default)
        {
            return _receptionWorkflow.GetReceptionMessagesAsync(staffId, roleCode, conversationId, beforeMessageId, take, cancellationToken);
        }

        public Task<ChatMessageDto> SendStaffMessageAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            ChatSendMessageRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return _receptionWorkflow.SendStaffMessageAsync(staffId, roleCode, conversationId, request, cancellationToken);
        }

        public Task MarkStaffReadAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            long? lastReadMessageId,
            CancellationToken cancellationToken = default)
        {
            return _receptionWorkflow.MarkStaffReadAsync(staffId, roleCode, conversationId, lastReadMessageId, cancellationToken);
        }
    }
}
