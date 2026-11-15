using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Chat;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.DTOs.Responses.Chat;
using MyGarage.Exceptions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ChatV2ReceptionWorkflowService : IChatV2ReceptionWorkflowService
    {
        private readonly IChatStore _store;
        private readonly IChatRealtimeNotifier _notifier;

        public ChatV2ReceptionWorkflowService(IChatStore store, IChatRealtimeNotifier notifier)
        {
            _store = store;
            _notifier = notifier;
        }

        public async Task<PagedConversationsResponseDto> GetReceptionConversationsAsync(
            long staffId,
            string? roleCode,
            long? branchId,
            string? status,
            bool? isUnread,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var targetBranch = ResolveReceptionistBranchScope(staff, branchId);
            var safePage = page <= 0 ? 1 : page;
            var safePageSize = pageSize <= 0 ? ChatV2MessagingSupport.DefaultPageSize : pageSize;
            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();

            var rows = await _store.GetReceptionConversationsPageAsync(
                staffId,
                targetBranch,
                normalizedStatus,
                isUnread,
                safePage,
                safePageSize,
                cancellationToken);

            var items = rows.Select(x => new ConversationListItemDto
            {
                Id = x.Conversation.Id,
                CustomerId = x.Conversation.CustomerId,
                CustomerFullName = x.CustomerFullName,
                StaffId = x.Conversation.StaffId,
                AssignedStaffId = x.Conversation.AssignedStaffId,
                BranchId = x.Conversation.BranchId,
                Status = x.Conversation.Status ?? nameof(ConversationStatus.OPEN),
                LastMessage = x.Conversation.LastMessage,
                LastMessageAt = ChatV2MessagingSupport.ToChatDisplayTime(x.Conversation.LastMessageAt ?? x.Conversation.UpdatedAt ?? x.Conversation.CreatedAt),
                UnreadCount = x.UnreadCount,
                CustomerOnline = _notifier.IsCustomerOnline(x.Conversation.CustomerId)
            }).ToList();

            return new PagedConversationsResponseDto
            {
                Items = items,
                Page = safePage,
                PageSize = safePageSize
            };
        }

        public async Task<ConversationDetailDto?> GetReceptionConversationAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken);
            if (conversation is null) return null;
            return await ChatV2MessagingSupport.BuildDetailAsync(
                _store,
                _notifier,
                conversation,
                await _store.GetUnreadForStaffAsync(conversation.Id, staffId, cancellationToken),
                cancellationToken);
        }

        public async Task<IReadOnlyList<ChatOrderCardDto>> GetReceptionConversationOrdersAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken);
            if (conversation is null)
            {
                throw new KeyNotFoundException("Conversation not found.");
            }

            return await ChatV2OrderCardSupport.BuildConversationOrderCardsAsync(_store, conversation.CustomerId, forReceptionist: true, cancellationToken);
        }

        public async Task<ConversationDetailDto> UpdateConversationStatusAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            string status,
            string? reason,
            CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken)
                ?? throw new KeyNotFoundException("Conversation not found.");
            var normalized = ChatV2MessagingSupport.NormalizeConversationStatus(status);
            var currentStatus = ChatV2MessagingSupport.NormalizeStoredConversationStatus(conversation.Status);
            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (!CanTransitionByReceptionist(currentStatus, normalized))
            {
                throw new ChatConflictException($"Cannot change conversation status from {currentStatus} to {normalized}.");
            }

            if (currentStatus == normalized)
            {
                return await ChatV2MessagingSupport.BuildDetailAsync(
                    _store,
                    _notifier,
                    conversation,
                    await _store.GetUnreadForStaffAsync(conversation.Id, staffId, cancellationToken),
                    cancellationToken);
            }

            conversation.Status = normalized;
            conversation.UpdatedAt = DateTime.UtcNow;
            ChatV2MessagingSupport.AddChatAuditLog(
                _store,
                actorId: staffId,
                actorType: nameof(ChatSenderType.STAFF),
                branchId: conversation.BranchId,
                action: "CHAT_STATUS_CHANGE",
                conversationId: conversation.Id,
                oldValue: new { status = currentStatus },
                newValue: new { status = normalized, reason = normalizedReason });

            await _store.SaveChangesWithConcurrencyRetryAsync(cancellationToken);
            await _notifier.NotifyConversationUpdatedAsync(conversation.Id, conversation.BranchId, cancellationToken);
            return await ChatV2MessagingSupport.BuildDetailAsync(
                _store,
                _notifier,
                conversation,
                await _store.GetUnreadForStaffAsync(conversation.Id, staffId, cancellationToken),
                cancellationToken);
        }

        public async Task<ConversationDetailDto> AssignConversationAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            long assignedStaffId,
            CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken)
                ?? throw new KeyNotFoundException("Conversation not found.");

            var assignedStaff = await _store.GetActiveStaffWithRoleAsync(assignedStaffId, cancellationToken)
                ?? throw new KeyNotFoundException("Assigned staff not found.");

            if (!IsReceptionistRole(assignedStaff.Role?.RoleCode, assignedStaff.Role?.RoleName))
            {
                throw new InvalidOperationException("Assigned staff must be receptionist.");
            }

            if (!staff.BranchId.HasValue || !assignedStaff.BranchId.HasValue || staff.BranchId.Value != assignedStaff.BranchId.Value)
            {
                throw new UnauthorizedAccessException("Cannot assign conversation to another branch.");
            }

            var previousAssignedStaffId = conversation.AssignedStaffId;
            if (previousAssignedStaffId == assignedStaffId)
            {
                return await ChatV2MessagingSupport.BuildDetailAsync(
                    _store,
                    _notifier,
                    conversation,
                    await _store.GetUnreadForStaffAsync(conversation.Id, staffId, cancellationToken),
                    cancellationToken);
            }

            conversation.AssignedStaffId = assignedStaffId;
            conversation.UpdatedAt = DateTime.UtcNow;
            ChatV2MessagingSupport.AddChatAuditLog(
                _store,
                actorId: staffId,
                actorType: nameof(ChatSenderType.STAFF),
                branchId: conversation.BranchId,
                action: "CHAT_ASSIGN_RECEPTIONIST",
                conversationId: conversation.Id,
                oldValue: new { assignedStaffId = previousAssignedStaffId },
                newValue: new { assignedStaffId });

            await _store.SaveChangesWithConcurrencyRetryAsync(cancellationToken);
            await _notifier.NotifyConversationUpdatedAsync(conversation.Id, conversation.BranchId, cancellationToken);
            return await ChatV2MessagingSupport.BuildDetailAsync(
                _store,
                _notifier,
                conversation,
                await _store.GetUnreadForStaffAsync(conversation.Id, staffId, cancellationToken),
                cancellationToken);
        }

        public async Task<IReadOnlyList<ChatMessageDto>> GetReceptionMessagesAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            long? beforeMessageId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken);
            if (conversation is null) throw new KeyNotFoundException("Conversation not found.");
            return await ChatV2MessagingSupport.GetMessagesAsync(_store, conversation.Id, beforeMessageId, take, cancellationToken);
        }

        public async Task<ChatMessageDto> SendStaffMessageAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            ChatSendMessageRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken)
                ?? throw new KeyNotFoundException("Conversation not found.");

            return await ChatV2MessagingSupport.SendMessageInternalAsync(
                _store,
                _notifier,
                conversation,
                staffId,
                nameof(ChatSenderType.STAFF),
                nameof(ConversationStatus.WAITING_FOR_CUSTOMER),
                request,
                cancellationToken);
        }

        public async Task MarkStaffReadAsync(
            long staffId,
            string? roleCode,
            long conversationId,
            long? lastReadMessageId,
            CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken)
                ?? throw new KeyNotFoundException("Conversation not found.");
            var effectiveLastReadMessageId = await UpsertStaffReadAsync(conversation.Id, staffId, lastReadMessageId, cancellationToken);
            await _store.SaveChangesWithConcurrencyRetryAsync(cancellationToken);
            await _notifier.NotifyReadStateUpdatedAsync(
                conversation.Id,
                conversation.BranchId,
                nameof(ChatSenderType.STAFF),
                staffId,
                effectiveLastReadMessageId,
                cancellationToken);
        }

        private async Task<long?> UpsertStaffReadAsync(long conversationId, long staffId, long? lastReadMessageId, CancellationToken cancellationToken)
        {
            var target = await ChatV2MessagingSupport.ResolveValidLastReadMessageIdAsync(_store, conversationId, lastReadMessageId, cancellationToken);
            if (!target.HasValue) return null;
            var now = DateTime.UtcNow;
            var read = await _store.GetStaffReadAsync(conversationId, staffId, cancellationToken);
            if (read is null)
            {
                _store.AddStaffRead(new ConversationStaffRead { ConversationId = conversationId, StaffId = staffId, LastReadMessageId = target, LastReadAt = now, UpdatedAt = now });
                return target;
            }
            if (!read.LastReadMessageId.HasValue || target.Value > read.LastReadMessageId.Value) read.LastReadMessageId = target.Value;
            read.LastReadAt = now;
            read.UpdatedAt = now;
            return read.LastReadMessageId;
        }

        private async Task<Conversation?> GetConversationForStaffAsync(Staff staff, long conversationId, CancellationToken cancellationToken)
        {
            var c = await _store.GetConversationWithCustomerByIdAsync(conversationId, track: true, cancellationToken);
            if (c is null) return null;
            if (!staff.BranchId.HasValue || !c.BranchId.HasValue || c.BranchId.Value != staff.BranchId.Value)
                throw new UnauthorizedAccessException("Conversation is outside your branch.");
            return c;
        }

        private async Task<Staff> RequireStaffAsync(long staffId, string? roleCode, CancellationToken cancellationToken)
        {
            var staff = await _store.GetActiveStaffWithRoleAsync(staffId, cancellationToken);
            if (staff is null) throw new UnauthorizedAccessException("Staff not found.");

            if (!IsReceptionistRole(roleCode, staff.Role?.RoleCode, staff.Role?.RoleName))
            {
                throw new UnauthorizedAccessException("Only receptionist can access customer chat.");
            }

            return staff;
        }

        private static long ResolveReceptionistBranchScope(Staff staff, long? requestedBranchId)
        {
            if (!staff.BranchId.HasValue) throw new UnauthorizedAccessException("Staff does not have branch.");
            if (requestedBranchId.HasValue && requestedBranchId.Value != staff.BranchId.Value) throw new UnauthorizedAccessException("Cannot query another branch.");
            return staff.BranchId.Value;
        }

        private static bool IsReceptionistRole(params string?[] values)
        {
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var normalized = value.Trim().ToUpperInvariant();
                var compact = CompactRoleValue(normalized);

                if (normalized == "RECEPTIONIST"
                    || normalized.Contains("RECEPTION", StringComparison.OrdinalIgnoreCase)
                    || compact.Contains("RECEPTION", StringComparison.OrdinalIgnoreCase)
                    || compact.Contains("LETAN", StringComparison.OrdinalIgnoreCase)
                    || compact.Contains("TIEPTAN", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string CompactRoleValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var decomposed = value.Normalize(NormalizationForm.FormD);
            var chars = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (unicodeCategory == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(ch))
                {
                    chars.Append(char.ToUpperInvariant(ch));
                }
            }

            return chars.ToString().Normalize(NormalizationForm.FormC);
        }

        private static bool CanTransitionByReceptionist(string currentStatus, string targetStatus)
        {
            if (currentStatus == targetStatus)
            {
                return true;
            }

            return currentStatus switch
            {
                nameof(ConversationStatus.OPEN) => targetStatus is nameof(ConversationStatus.WAITING_FOR_STAFF) or nameof(ConversationStatus.WAITING_FOR_CUSTOMER) or nameof(ConversationStatus.CLOSED),
                nameof(ConversationStatus.WAITING_FOR_STAFF) => targetStatus is nameof(ConversationStatus.WAITING_FOR_CUSTOMER) or nameof(ConversationStatus.CLOSED),
                nameof(ConversationStatus.WAITING_FOR_CUSTOMER) => targetStatus is nameof(ConversationStatus.WAITING_FOR_STAFF) or nameof(ConversationStatus.CLOSED),
                nameof(ConversationStatus.CLOSED) => false,
                _ => false
            };
        }
    }
}
