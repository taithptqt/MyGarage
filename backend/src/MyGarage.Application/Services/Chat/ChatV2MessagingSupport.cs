using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
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
    internal static class ChatV2MessagingSupport
    {
        internal const int DefaultPageSize = 20;
        internal const int DefaultMessageTake = 50;
        private const int MaxAttachmentPerMessage = 10;
        private static readonly TimeZoneInfo ChatDisplayTimeZone = ResolveChatDisplayTimeZone();

        internal static string FormatCurrencyVnd(decimal? amount)
        {
            var safe = amount ?? 0m;
            var text = safe.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
            return $"{text}d";
        }

        internal static string NormalizeToken(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        internal static string SafeText(string? value, string fallback)
        {
            var normalized = (value ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        internal static async Task<ConversationDetailDto> BuildDetailAsync(
            IChatStore store,
            IChatRealtimeNotifier notifier,
            Conversation c,
            int unreadCount,
            CancellationToken cancellationToken)
        {
            return new ConversationDetailDto
            {
                Id = c.Id,
                CustomerId = c.CustomerId,
                StaffId = c.StaffId,
                AssignedStaffId = c.AssignedStaffId,
                BranchId = c.BranchId,
                AppointmentId = c.AppointmentId,
                ServiceOrderId = c.ServiceOrderId,
                VehicleId = c.VehicleId,
                Status = c.Status ?? nameof(ConversationStatus.OPEN),
                LastMessage = c.LastMessage,
                LastMessageAt = ToChatDisplayTime(c.LastMessageAt ?? c.UpdatedAt ?? c.CreatedAt),
                UnreadCount = unreadCount,
                CustomerOnline = notifier.IsCustomerOnline(c.CustomerId),
                CustomerName = c.Customer?.FullName,
                CustomerPhone = c.Customer?.Phone,
                Messages = await GetMessagesAsync(store, c.Id, null, DefaultMessageTake, cancellationToken)
            };
        }

        internal static async Task<ChatMessageDto> SendMessageInternalAsync(
            IChatStore store,
            IChatRealtimeNotifier notifier,
            Conversation conversation,
            long senderId,
            string senderType,
            string targetStatus,
            ChatSendMessageRequestDto request,
            CancellationToken cancellationToken)
        {
            request ??= new ChatSendMessageRequestDto();
            var hasOrderCard = request.OrderCard is not null;
            var normalizedOrderCard = hasOrderCard ? ChatV2OrderCardSupport.NormalizeOrderCardRequest(request.OrderCard!) : null;
            var content = hasOrderCard
                ? string.Empty
                : (request.Content ?? string.Empty).Trim();
            var attachmentUrls = NormalizeAttachmentUrls(request.AttachmentUrls, request.AttachmentUrl);
            if (string.IsNullOrWhiteSpace(content) && attachmentUrls.Count == 0 && normalizedOrderCard is null)
                throw new InvalidOperationException("Message content or attachment is required.");
            if (normalizedOrderCard is not null && attachmentUrls.Count > 0)
            {
                throw new InvalidOperationException("Order card message cannot include attachments.");
            }

            if (request.ClientMessageId.HasValue)
            {
                var dup = await store.GetChatMessageByClientMessageIdAsync(request.ClientMessageId.Value, cancellationToken);
                if (dup is not null)
                {
                    if (dup.ConversationId != conversation.Id)
                    {
                        throw new ChatConflictException("ClientMessageId already exists in another conversation.");
                    }

                    var existingAttachmentLookup = await GetAttachmentLookupAsync(store, new[] { dup.Id }, cancellationToken);
                    existingAttachmentLookup.TryGetValue(dup.Id, out var existingAttachments);
                    return MapMessage(dup, existingAttachments);
                }
            }

            var currentStatus = NormalizeStoredConversationStatus(conversation.Status);
            var nextStatus = ResolveStatusAfterMessage(currentStatus, senderType, targetStatus);

            var now = DateTime.UtcNow;
            var hasImageOnlyAttachments = AreImageOnlyAttachmentUrls(attachmentUrls);
            // Persist order card messages as TEXT to stay compatible with legacy DB
            // check constraints that only allow TEXT/IMAGE/SYSTEM.
            var normalizedMessageType = normalizedOrderCard is not null
                ? nameof(ChatMessageType.TEXT)
                : NormalizeMessageType(
                    request.MessageType,
                    attachmentUrls.Count > 0,
                    hasImageOnlyAttachments);
            var storedContent = normalizedOrderCard is not null
                ? JsonSerializer.Serialize(ChatV2OrderCardSupport.ToStoredOrderCardPayload(normalizedOrderCard), ChatV2OrderCardSupport.OrderCardSerializerOptions)
                : content;
            var message = new ChatMessage
            {
                ConversationId = conversation.Id,
                ClientMessageId = request.ClientMessageId,
                SenderId = senderId,
                SenderType = senderType,
                MessageType = normalizedMessageType,
                Content = storedContent,
                AttachmentUrl = attachmentUrls.Count > 0 ? attachmentUrls[0] : null,
                CreatedAt = now,
                IsDeleted = false
            };

            if (attachmentUrls.Count > 0)
            {
                for (var i = 0; i < attachmentUrls.Count; i++)
                {
                    var attachmentUrl = attachmentUrls[i];
                    message.ChatMessageAttachments.Add(new ChatMessageAttachment
                    {
                        AttachmentUrl = attachmentUrl,
                        ContentType = InferContentTypeFromAttachmentUrl(attachmentUrl),
                        FileSize = null,
                        SortOrder = i + 1,
                        CreatedAt = now
                    });
                }
            }

            store.AddChatMessage(message);
            conversation.Status = nextStatus;
            conversation.LastMessage = normalizedOrderCard is not null
                ? ChatV2OrderCardSupport.BuildOrderCardConversationPreview(normalizedOrderCard)
                : !string.IsNullOrWhiteSpace(content)
                    ? content
                    : hasImageOnlyAttachments
                        ? attachmentUrls.Count > 1
                            ? $"[{attachmentUrls.Count} anh]"
                            : "[Anh]"
                        : attachmentUrls.Count > 1
                            ? $"[{attachmentUrls.Count} tep dinh kem]"
                            : "[Tep dinh kem]";
            conversation.LastSenderId = senderId;
            conversation.LastMessageAt = now;
            conversation.UpdatedAt = now;

            if (string.Equals(currentStatus, nameof(ConversationStatus.CLOSED), StringComparison.OrdinalIgnoreCase)
                && string.Equals(senderType, nameof(ChatSenderType.CUSTOMER), StringComparison.OrdinalIgnoreCase))
            {
                AddChatAuditLog(
                    store,
                    actorId: senderId,
                    actorType: nameof(ChatSenderType.CUSTOMER),
                    branchId: conversation.BranchId,
                    action: "CHAT_REOPEN_BY_CUSTOMER",
                    conversationId: conversation.Id,
                    oldValue: new { status = currentStatus },
                    newValue: new { status = nextStatus });
            }

            await store.SaveChangesWithConcurrencyRetryAsync(cancellationToken);

            conversation.LastMessageId = message.Id;
            await store.SaveChangesWithConcurrencyRetryAsync(cancellationToken);

            var dto = MapMessage(message, BuildAttachmentDtosFromEntity(message.ChatMessageAttachments));
            await notifier.NotifyNewMessageAsync(conversation.Id, conversation.BranchId, dto, cancellationToken);
            return dto;
        }

        internal static async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
            IChatStore store,
            long conversationId,
            long? beforeMessageId,
            int take,
            CancellationToken cancellationToken)
        {
            var safeTake = take <= 0 ? DefaultMessageTake : take;
            var list = await store.GetConversationMessagesAsync(conversationId, beforeMessageId, safeTake, cancellationToken);
            list.Reverse();
            if (list.Count == 0)
            {
                return Array.Empty<ChatMessageDto>();
            }

            var attachmentLookup = await GetAttachmentLookupAsync(store, list.Select(x => x.Id).ToArray(), cancellationToken);
            var result = new List<ChatMessageDto>(list.Count);
            foreach (var message in list)
            {
                attachmentLookup.TryGetValue(message.Id, out var attachments);
                result.Add(MapMessage(message, attachments));
            }

            return result;
        }

        internal static async Task<long?> ResolveValidLastReadMessageIdAsync(
            IChatStore store,
            long conversationId,
            long? requestedLastReadMessageId,
            CancellationToken cancellationToken)
        {
            if (!requestedLastReadMessageId.HasValue)
            {
                return await store.GetMaxChatMessageIdAsync(conversationId, cancellationToken);
            }

            var exists = await store.ChatMessageExistsInConversationAsync(
                requestedLastReadMessageId.Value,
                conversationId,
                cancellationToken);

            if (!exists)
            {
                throw new ChatConflictException("LastReadMessageId is invalid for this conversation.");
            }

            return requestedLastReadMessageId.Value;
        }

        private static async Task<Dictionary<long, IReadOnlyList<ChatMessageAttachmentDto>>> GetAttachmentLookupAsync(
            IChatStore store,
            IReadOnlyCollection<long> messageIds,
            CancellationToken cancellationToken)
        {
            if (messageIds.Count == 0)
            {
                return new Dictionary<long, IReadOnlyList<ChatMessageAttachmentDto>>();
            }

            var attachments = await store.GetAttachmentsByMessageIdsAsync(messageIds, cancellationToken);

            return attachments
                .GroupBy(x => x.MessageId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<ChatMessageAttachmentDto>)g.Select(MapAttachment).ToList());
        }

        private static IReadOnlyList<ChatMessageAttachmentDto> BuildAttachmentDtosFromEntity(IEnumerable<ChatMessageAttachment>? attachments)
        {
            if (attachments is null)
            {
                return Array.Empty<ChatMessageAttachmentDto>();
            }

            return attachments
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .Select(MapAttachment)
                .ToList();
        }

        private static ChatMessageAttachmentDto MapAttachment(ChatMessageAttachment attachment)
        {
            return new ChatMessageAttachmentDto
            {
                Id = attachment.Id,
                AttachmentUrl = attachment.AttachmentUrl,
                ContentType = attachment.ContentType,
                FileSize = attachment.FileSize,
                SortOrder = attachment.SortOrder,
                CreatedAt = ToChatDisplayTime(attachment.CreatedAt)
            };
        }

        private static ChatMessageDto MapMessage(ChatMessage m, IReadOnlyList<ChatMessageAttachmentDto>? attachments = null)
        {
            var resolvedAttachments = attachments ?? BuildAttachmentDtosFromEntity(m.ChatMessageAttachments);
            var fallbackAttachmentUrl = resolvedAttachments.FirstOrDefault()?.AttachmentUrl;
            var messageType = NormalizeMessageType(m.MessageType);
            var orderCard = ChatV2OrderCardSupport.TryParseStoredOrderCard(messageType, m.Content);
            var responseMessageType = orderCard is not null
                ? nameof(ChatMessageType.ORDER_CARD)
                : messageType;
            return new ChatMessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderType = m.SenderType,
                MessageType = responseMessageType,
                Content = orderCard is not null ? orderCard.Title : m.Content,
                AttachmentUrl = string.IsNullOrWhiteSpace(m.AttachmentUrl) ? fallbackAttachmentUrl : m.AttachmentUrl,
                Attachments = resolvedAttachments,
                OrderCard = orderCard,
                CreatedAt = ToChatDisplayTime(m.CreatedAt)
            };
        }

        internal static string? NormalizeDetailUrl(string? detailUrl)
        {
            var normalized = (detailUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (!normalized.StartsWith("/", StringComparison.Ordinal))
            {
                return null;
            }

            return normalized.Length <= 300 ? normalized : normalized.Substring(0, 300);
        }

        private static string NormalizeMessageType(
            string? messageType,
            bool hasAttachments = false,
            bool hasImageOnlyAttachments = false)
        {
            var normalized = (messageType ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch
            {
                nameof(ChatMessageType.IMAGE) when hasAttachments && !hasImageOnlyAttachments => nameof(ChatMessageType.TEXT),
                nameof(ChatMessageType.IMAGE) => nameof(ChatMessageType.IMAGE),
                nameof(ChatMessageType.SYSTEM) => nameof(ChatMessageType.SYSTEM),
                // Legacy DB constraints may reject ORDER_CARD, so normalize to TEXT at persistence layer.
                nameof(ChatMessageType.ORDER_CARD) => nameof(ChatMessageType.TEXT),
                nameof(ChatMessageType.TEXT) => nameof(ChatMessageType.TEXT),
                _ => hasAttachments && hasImageOnlyAttachments
                    ? nameof(ChatMessageType.IMAGE)
                    : nameof(ChatMessageType.TEXT)
            };
        }

        private static bool AreImageOnlyAttachmentUrls(IReadOnlyList<string> attachmentUrls)
        {
            if (attachmentUrls.Count == 0)
            {
                return false;
            }

            foreach (var attachmentUrl in attachmentUrls)
            {
                if (!IsImageAttachmentUrl(attachmentUrl))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsImageAttachmentUrl(string attachmentUrl)
        {
            var contentType = InferContentTypeFromAttachmentUrl(attachmentUrl);
            return !string.IsNullOrWhiteSpace(contentType)
                && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> NormalizeAttachmentUrls(IReadOnlyList<string>? attachmentUrls, string? legacyAttachmentUrl)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (attachmentUrls is not null)
            {
                foreach (var attachmentUrl in attachmentUrls)
                {
                    var normalized = (attachmentUrl ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(normalized))
                    {
                        continue;
                    }

                    if (seen.Add(normalized))
                    {
                        result.Add(normalized);
                    }
                }
            }

            var legacy = (legacyAttachmentUrl ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(legacy) && seen.Add(legacy))
            {
                result.Add(legacy);
            }

            if (result.Count > MaxAttachmentPerMessage)
            {
                throw new InvalidOperationException($"Maximum {MaxAttachmentPerMessage} attachments are allowed per message.");
            }

            return result;
        }

        private static string? InferContentTypeFromAttachmentUrl(string attachmentUrl)
        {
            var value = (attachmentUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var path = value;
            var queryStart = path.IndexOf('?', StringComparison.Ordinal);
            if (queryStart >= 0)
            {
                path = path.Substring(0, queryStart);
            }

            var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return extension switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".zip" => "application/zip",
                ".rar" => "application/vnd.rar",
                _ => null
            };
        }

        internal static void AddChatAuditLog(
            IChatStore store,
            long actorId,
            string actorType,
            long? branchId,
            string action,
            long conversationId,
            object? oldValue,
            object? newValue)
        {
            store.AddAuditLog(new AuditLog
            {
                UserId = actorId,
                UserType = actorType.Trim().ToUpperInvariant(),
                BranchId = branchId,
                Action = action,
                TableName = "Conversations",
                RecordId = conversationId,
                OldValue = SerializeAuditObject(oldValue),
                NewValue = SerializeAuditObject(newValue),
                Timestamp = DateTime.UtcNow
            });
        }

        private static string? SerializeAuditObject(object? value)
        {
            return value is null ? null : JsonSerializer.Serialize(value);
        }

        internal static string NormalizeConversationStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch
            {
                nameof(ConversationStatus.OPEN) => nameof(ConversationStatus.OPEN),
                nameof(ConversationStatus.WAITING_FOR_STAFF) => nameof(ConversationStatus.WAITING_FOR_STAFF),
                nameof(ConversationStatus.WAITING_FOR_CUSTOMER) => nameof(ConversationStatus.WAITING_FOR_CUSTOMER),
                nameof(ConversationStatus.CLOSED) => nameof(ConversationStatus.CLOSED),
                _ => throw new InvalidOperationException("Invalid conversation status.")
            };
        }

        internal static string NormalizeStoredConversationStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return nameof(ConversationStatus.OPEN);
            }

            try
            {
                return NormalizeConversationStatus(status);
            }
            catch (InvalidOperationException)
            {
                throw new ChatConflictException("Conversation has invalid status.");
            }
        }

        private static string ResolveStatusAfterMessage(string currentStatus, string senderType, string desiredTargetStatus)
        {
            var normalizedSenderType = (senderType ?? string.Empty).Trim().ToUpperInvariant();
            var normalizedDesiredTarget = NormalizeConversationStatus(desiredTargetStatus);

            if (normalizedSenderType == nameof(ChatSenderType.CUSTOMER))
            {
                if (normalizedDesiredTarget != nameof(ConversationStatus.WAITING_FOR_STAFF))
                {
                    throw new ChatConflictException("Invalid target status for customer message.");
                }

                return currentStatus switch
                {
                    nameof(ConversationStatus.OPEN) => nameof(ConversationStatus.WAITING_FOR_STAFF),
                    nameof(ConversationStatus.WAITING_FOR_STAFF) => nameof(ConversationStatus.WAITING_FOR_STAFF),
                    nameof(ConversationStatus.WAITING_FOR_CUSTOMER) => nameof(ConversationStatus.WAITING_FOR_STAFF),
                    nameof(ConversationStatus.CLOSED) => nameof(ConversationStatus.WAITING_FOR_STAFF),
                    _ => throw new ChatConflictException($"Cannot send message from status {currentStatus}.")
                };
            }

            if (normalizedSenderType == nameof(ChatSenderType.STAFF))
            {
                if (normalizedDesiredTarget != nameof(ConversationStatus.WAITING_FOR_CUSTOMER))
                {
                    throw new ChatConflictException("Invalid target status for receptionist message.");
                }

                return currentStatus switch
                {
                    nameof(ConversationStatus.OPEN) => nameof(ConversationStatus.WAITING_FOR_CUSTOMER),
                    nameof(ConversationStatus.WAITING_FOR_STAFF) => nameof(ConversationStatus.WAITING_FOR_CUSTOMER),
                    nameof(ConversationStatus.WAITING_FOR_CUSTOMER) => nameof(ConversationStatus.WAITING_FOR_CUSTOMER),
                    nameof(ConversationStatus.CLOSED) => throw new ChatConflictException("Cannot send to closed conversation."),
                    _ => throw new ChatConflictException($"Cannot send message from status {currentStatus}.")
                };
            }

            return normalizedDesiredTarget;
        }

        internal static DateTime? ToChatDisplayTime(DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            var source = value.Value;
            var utc = source.Kind switch
            {
                DateTimeKind.Utc => source,
                DateTimeKind.Local => source.ToUniversalTime(),
                _ => DateTime.SpecifyKind(source, DateTimeKind.Utc)
            };

            return TimeZoneInfo.ConvertTimeFromUtc(utc, ChatDisplayTimeZone);
        }

        private static TimeZoneInfo ResolveChatDisplayTimeZone()
        {
            var ids = new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" };
            foreach (var id in ids)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.Local;
        }
    }
}
