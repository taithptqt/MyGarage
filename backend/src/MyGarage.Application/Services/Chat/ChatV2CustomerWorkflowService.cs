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
    public class ChatV2CustomerWorkflowService : IChatV2CustomerWorkflowService
    {
        private readonly IChatStore _store;
        private readonly IChatRealtimeNotifier _notifier;

        public ChatV2CustomerWorkflowService(IChatStore store, IChatRealtimeNotifier notifier)
        {
            _store = store;
            _notifier = notifier;
        }

        public async Task<PagedConversationsResponseDto> GetCustomerConversationsAsync(
            long customerId,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var safePage = page <= 0 ? 1 : page;
            var safePageSize = pageSize <= 0 ? ChatV2MessagingSupport.DefaultPageSize : pageSize;
            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();

            var list = await _store.GetCustomerConversationsPageAsync(customerId, normalizedStatus, safePage, safePageSize, cancellationToken);

            var items = new List<ConversationListItemDto>(list.Count);
            foreach (var c in list)
            {
                items.Add(new ConversationListItemDto
                {
                    Id = c.Id,
                    CustomerId = c.CustomerId,
                    CustomerFullName = null,
                    StaffId = c.StaffId,
                    AssignedStaffId = c.AssignedStaffId,
                    BranchId = c.BranchId,
                    Status = c.Status ?? nameof(ConversationStatus.OPEN),
                    LastMessage = c.LastMessage,
                    LastMessageAt = ChatV2MessagingSupport.ToChatDisplayTime(c.LastMessageAt ?? c.UpdatedAt ?? c.CreatedAt),
                    UnreadCount = await _store.GetUnreadForCustomerAsync(c.Id, customerId, cancellationToken),
                    CustomerOnline = _notifier.IsCustomerOnline(c.CustomerId)
                });
            }

            return new PagedConversationsResponseDto { Items = items, Page = safePage, PageSize = safePageSize };
        }

        public async Task<ConversationDetailDto?> GetCustomerConversationAsync(
            long customerId,
            long conversationId,
            CancellationToken cancellationToken = default)
        {
            var conversation = await _store.GetCustomerConversationWithCustomerAsync(customerId, conversationId, cancellationToken);
            if (conversation is null) return null;

            return await ChatV2MessagingSupport.BuildDetailAsync(
                _store,
                _notifier,
                conversation,
                await _store.GetUnreadForCustomerAsync(conversation.Id, customerId, cancellationToken),
                cancellationToken);
        }

        public async Task<ConversationDetailDto> CreateCustomerConversationAsync(
            long customerId,
            ChatConversationCreateRequestDto request,
            CancellationToken cancellationToken = default)
        {
            request ??= new ChatConversationCreateRequestDto();
            var targetBranchId = request.BranchId > 0
                ? request.BranchId
                : await ResolveCustomerMessengerBranchIdAsync(customerId, null, cancellationToken);
            var now = DateTime.UtcNow;

            var existing = await _store.FindOpenCustomerConversationAsync(
                customerId,
                targetBranchId,
                request.AppointmentId,
                request.ServiceOrderId,
                request.VehicleId,
                cancellationToken);

            if (existing is null)
            {
                existing = new Conversation
                {
                    CustomerId = customerId,
                    BranchId = targetBranchId,
                    AppointmentId = request.AppointmentId,
                    ServiceOrderId = request.ServiceOrderId,
                    VehicleId = request.VehicleId,
                    Status = nameof(ConversationStatus.WAITING_FOR_STAFF),
                    CreatedAt = now,
                    UpdatedAt = now,
                    LastMessageAt = now
                };
                _store.AddConversation(existing);
                await _store.SaveChangesWithConcurrencyRetryAsync(cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(request.InitialMessage))
            {
                await SendCustomerMessageAsync(
                    customerId,
                    existing.Id,
                    new ChatSendMessageRequestDto { Content = request.InitialMessage },
                    cancellationToken);
            }

            var refreshed = await _store.GetConversationWithCustomerByIdAsync(existing.Id, track: true, cancellationToken)
                ?? throw new ChatConflictException("Conversation no longer exists.");
            return await ChatV2MessagingSupport.BuildDetailAsync(
                _store,
                _notifier,
                refreshed,
                await _store.GetUnreadForCustomerAsync(refreshed.Id, customerId, cancellationToken),
                cancellationToken);
        }

        public async Task<ConversationDetailDto> GetOrCreateCustomerMessengerConversationAsync(
            long customerId,
            long? preferredBranchId,
            CancellationToken cancellationToken = default)
        {
            var conversation = await _store.GetLatestCustomerConversationAsync(customerId, excludeClosed: true, cancellationToken)
                ?? await _store.GetLatestCustomerConversationAsync(customerId, excludeClosed: false, cancellationToken);

            if (conversation is null)
            {
                var targetBranchId = await ResolveCustomerMessengerBranchIdAsync(customerId, preferredBranchId, cancellationToken);
                var now = DateTime.UtcNow;

                conversation = new Conversation
                {
                    CustomerId = customerId,
                    BranchId = targetBranchId,
                    Status = nameof(ConversationStatus.WAITING_FOR_STAFF),
                    CreatedAt = now,
                    UpdatedAt = now,
                    LastMessageAt = now
                };

                _store.AddConversation(conversation);
                await _store.SaveChangesWithConcurrencyRetryAsync(cancellationToken);
            }

            var refreshed = await _store.GetConversationWithCustomerByIdAsync(conversation.Id, track: true, cancellationToken)
                ?? throw new ChatConflictException("Conversation no longer exists.");
            var unreadCount = await _store.GetUnreadForCustomerAsync(refreshed.Id, customerId, cancellationToken);
            return await ChatV2MessagingSupport.BuildDetailAsync(_store, _notifier, refreshed, unreadCount, cancellationToken);
        }

        public async Task<IReadOnlyList<ChatOrderCardDto>> GetCustomerConversationOrdersAsync(
            long customerId,
            long conversationId,
            CancellationToken cancellationToken = default)
        {
            var allowed = await _store.CustomerOwnsConversationAsync(customerId, conversationId, cancellationToken);
            if (!allowed)
            {
                throw new UnauthorizedAccessException("Conversation access denied.");
            }

            return await ChatV2OrderCardSupport.BuildConversationOrderCardsAsync(_store, customerId, forReceptionist: false, cancellationToken);
        }

        public async Task<IReadOnlyList<ChatMessageDto>> GetCustomerMessagesAsync(
            long customerId,
            long conversationId,
            long? beforeMessageId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var allowed = await _store.CustomerOwnsConversationAsync(customerId, conversationId, cancellationToken);
            if (!allowed) throw new UnauthorizedAccessException("Conversation access denied.");
            return await ChatV2MessagingSupport.GetMessagesAsync(_store, conversationId, beforeMessageId, take, cancellationToken);
        }

        public async Task<ChatMessageDto> SendCustomerMessageAsync(
            long customerId,
            long conversationId,
            ChatSendMessageRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var conversation = await _store.GetCustomerConversationForUpdateAsync(customerId, conversationId, cancellationToken)
                ?? throw new UnauthorizedAccessException("Conversation access denied.");
            return await ChatV2MessagingSupport.SendMessageInternalAsync(
                _store,
                _notifier,
                conversation,
                customerId,
                nameof(ChatSenderType.CUSTOMER),
                nameof(ConversationStatus.WAITING_FOR_STAFF),
                request,
                cancellationToken);
        }

        public async Task MarkCustomerReadAsync(
            long customerId,
            long conversationId,
            long? lastReadMessageId,
            CancellationToken cancellationToken = default)
        {
            var conversation = await _store.GetCustomerConversationForUpdateAsync(customerId, conversationId, cancellationToken)
                ?? throw new UnauthorizedAccessException("Conversation access denied.");
            var effectiveLastReadMessageId = await UpsertCustomerReadAsync(conversation.Id, customerId, lastReadMessageId, cancellationToken);
            await _store.SaveChangesWithConcurrencyRetryAsync(cancellationToken);
            await _notifier.NotifyReadStateUpdatedAsync(
                conversation.Id,
                conversation.BranchId,
                nameof(ChatSenderType.CUSTOMER),
                customerId,
                effectiveLastReadMessageId,
                cancellationToken);
        }

        private async Task<long?> UpsertCustomerReadAsync(long conversationId, long customerId, long? lastReadMessageId, CancellationToken cancellationToken)
        {
            var target = await ChatV2MessagingSupport.ResolveValidLastReadMessageIdAsync(_store, conversationId, lastReadMessageId, cancellationToken);
            if (!target.HasValue) return null;
            var now = DateTime.UtcNow;
            var read = await _store.GetCustomerReadAsync(conversationId, cancellationToken);
            if (read is null)
            {
                _store.AddCustomerRead(new ConversationCustomerRead { ConversationId = conversationId, CustomerId = customerId, LastReadMessageId = target, LastReadAt = now, UpdatedAt = now });
                return target;
            }
            if (!read.LastReadMessageId.HasValue || target.Value > read.LastReadMessageId.Value) read.LastReadMessageId = target.Value;
            read.LastReadAt = now;
            read.UpdatedAt = now;
            return read.LastReadMessageId;
        }

        private async Task<long> ResolveCustomerMessengerBranchIdAsync(long customerId, long? preferredBranchId, CancellationToken cancellationToken)
        {
            var branches = await _store.GetActiveChatBranchesAsync(cancellationToken);

            if (branches.Count == 0)
            {
                throw new InvalidOperationException("No branch available to open messenger conversation.");
            }

            if (preferredBranchId.HasValue && branches.Any(b => b.Id == preferredBranchId.Value && IsBranchActive(b.Status)))
            {
                return preferredBranchId.Value;
            }

            var customerNote = await _store.GetCustomerNoteAsync(customerId, cancellationToken);

            var preferredBranchFromNote = ExtractPreferredBranchIdFromCustomerNote(customerNote);
            if (preferredBranchFromNote.HasValue && branches.Any(b => b.Id == preferredBranchFromNote.Value && IsBranchActive(b.Status)))
            {
                return preferredBranchFromNote.Value;
            }

            var cityFromNote = ExtractCityFromCustomerNote(customerNote);
            if (!string.IsNullOrWhiteSpace(cityFromNote) && TryResolveRegionFromProvince(cityFromNote, out var region))
            {
                var branchByRegion = FindBranchByRegion(branches, region);
                if (branchByRegion is not null)
                {
                    return branchByRegion.Id;
                }
            }

            var northBranch = FindNorthFallbackBranch(branches);
            if (northBranch is not null)
            {
                return northBranch.Id;
            }

            var activeBranch = branches
                .OrderByDescending(b => IsBranchActive(b.Status))
                .ThenBy(b => b.Id)
                .FirstOrDefault();

            return activeBranch?.Id ?? branches.OrderBy(b => b.Id).First().Id;
        }

        private static ChatBranchCandidate? FindBranchByRegion(IReadOnlyCollection<ChatBranchCandidate> branches, string region)
        {
            string[] regionKeywords = region switch
            {
                "north" => new[] { "ha noi", "hanoi", "bac", "north", "mien bac" },
                "central" => new[] { "da nang", "danang", "trung", "central", "mien trung" },
                "south" => new[] { "hcm", "ho chi minh", "sai gon", "nam", "south", "mien nam" },
                _ => Array.Empty<string>()
            };

            if (regionKeywords.Length == 0)
            {
                return null;
            }

            var ordered = branches
                .OrderByDescending(b => IsBranchActive(b.Status))
                .ThenBy(b => b.Id);

            foreach (var branch in ordered)
            {
                var searchable = $"{NormalizeVietnamText(branch.Name)} {NormalizeVietnamText(branch.Address)}";
                if (regionKeywords.Any(keyword => searchable.Contains(keyword, StringComparison.Ordinal)))
                {
                    return branch;
                }
            }

            return null;
        }

        private static ChatBranchCandidate? FindNorthFallbackBranch(IReadOnlyCollection<ChatBranchCandidate> branches)
        {
            var keywords = new[] { "bac", "ha noi", "hanoi", "north", "mien bac" };

            var ordered = branches
                .OrderByDescending(b => IsBranchActive(b.Status))
                .ThenBy(b => b.Id);

            foreach (var branch in ordered)
            {
                var searchable = $"{NormalizeVietnamText(branch.Name)} {NormalizeVietnamText(branch.Address)}";
                if (keywords.Any(keyword => searchable.Contains(keyword, StringComparison.Ordinal)))
                {
                    return branch;
                }
            }

            return null;
        }

        private static long? ExtractPreferredBranchIdFromCustomerNote(string? note)
        {
            var value = ExtractTagValue(note, "PreferredBranchId");
            return long.TryParse(value, out var branchId) ? branchId : null;
        }

        private static string? ExtractCityFromCustomerNote(string? note)
        {
            var city = ExtractTagValue(note, "City");
            return string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        }

        private static string? ExtractTagValue(string? source, string tagKey)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(tagKey))
            {
                return null;
            }

            var marker = $"[{tagKey}:";
            var start = source.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            var end = source.IndexOf("]", start, StringComparison.OrdinalIgnoreCase);
            if (end <= start)
            {
                return null;
            }

            return source.Substring(start, end - start);
        }

        private static bool IsBranchActive(string? status)
        {
            return string.IsNullOrWhiteSpace(status) || string.Equals(status.Trim(), "ACTIVE", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryResolveRegionFromProvince(string province, out string region)
        {
            var normalized = NormalizeVietnamText(province);
            normalized = normalized
                .Replace("tinh ", string.Empty, StringComparison.Ordinal)
                .Replace("thanh pho ", string.Empty, StringComparison.Ordinal)
                .Replace("tp ", string.Empty, StringComparison.Ordinal);

            var north = new HashSet<string>
            {
                "ha noi", "hai phong", "quang ninh", "bac ninh", "bac giang", "lang son", "cao bang",
                "bac kan", "thai nguyen", "tuyen quang", "ha giang", "lao cai", "yen bai", "dien bien",
                "lai chau", "son la", "hoa binh", "phu tho", "vinh phuc", "thai binh", "nam dinh",
                "ha nam", "ninh binh", "hung yen", "hai duong"
            };

            var central = new HashSet<string>
            {
                "thanh hoa", "nghe an", "ha tinh", "quang binh", "quang tri", "thua thien hue",
                "hue", "da nang", "quang nam", "quang ngai", "binh dinh", "phu yen", "khanh hoa",
                "ninh thuan", "binh thuan", "kon tum", "gia lai", "dak lak", "dak nong", "lam dong"
            };

            var south = new HashSet<string>
            {
                "ho chi minh", "tp hcm", "hcm", "sai gon", "ba ria vung tau", "vung tau",
                "binh duong", "binh phuoc", "dong nai", "tay ninh", "long an", "tien giang",
                "ben tre", "tra vinh", "vinh long", "dong thap", "an giang", "kien giang",
                "can tho", "hau giang", "soc trang", "bac lieu", "ca mau"
            };

            if (north.Contains(normalized))
            {
                region = "north";
                return true;
            }

            if (central.Contains(normalized))
            {
                region = "central";
                return true;
            }

            if (south.Contains(normalized))
            {
                region = "south";
                return true;
            }

            region = string.Empty;
            return false;
        }

        private static string NormalizeVietnamText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var buffer = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                buffer.Append(ch == '\u0111' ? 'd' : ch);
            }

            var noAccent = buffer.ToString().Normalize(NormalizationForm.FormC);
            var cleaned = new StringBuilder(noAccent.Length);
            var previousWasSpace = false;

            foreach (var ch in noAccent)
            {
                var current = char.IsLetterOrDigit(ch) ? ch : ' ';
                if (current == ' ')
                {
                    if (previousWasSpace)
                    {
                        continue;
                    }

                    previousWasSpace = true;
                }
                else
                {
                    previousWasSpace = false;
                }

                cleaned.Append(current);
            }

            return cleaned.ToString().Trim();
        }
    }
}
