using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.DTOs.Responses.Chat;
using MyGarage.Exceptions;
using MyGarage.Hubs;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ChatV2Service : IChatV2Service
    {
        private const int DefaultPageSize = 20;
        private const int DefaultMessageTake = 50;
        private const int MaxAttachmentPerMessage = 10;
        private const int MaxOrderCardsPerConversation = 30;
        private static readonly JsonSerializerOptions OrderCardSerializerOptions = new(JsonSerializerDefaults.Web);
        private static readonly TimeZoneInfo ChatDisplayTimeZone = ResolveChatDisplayTimeZone();
        private readonly IGarageContext _context;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatV2Service(IGarageContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<PagedConversationsResponseDto> GetCustomerConversationsAsync(long customerId, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var safePage = page <= 0 ? 1 : page;
            var safePageSize = pageSize <= 0 ? DefaultPageSize : pageSize;

            var query = _context.Conversations.AsNoTracking().Where(c => c.CustomerId == customerId);
            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToUpperInvariant();
                query = query.Where(c => c.Status == normalized);
            }

            var list = await query
                .OrderByDescending(c => c.LastMessageAt ?? c.UpdatedAt ?? c.CreatedAt)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .ToListAsync(cancellationToken);

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
                    LastMessageAt = ToChatDisplayTime(c.LastMessageAt ?? c.UpdatedAt ?? c.CreatedAt),
                    UnreadCount = await GetUnreadForCustomerAsync(c.Id, customerId, cancellationToken)
                });
            }

            return new PagedConversationsResponseDto { Items = items, Page = safePage, PageSize = safePageSize };
        }

        public async Task<ConversationDetailDto?> GetCustomerConversationAsync(long customerId, long conversationId, CancellationToken cancellationToken = default)
        {
            var conversation = await _context.Conversations
                .Include(c => c.Customer)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.CustomerId == customerId, cancellationToken);
            if (conversation is null) return null;

            return await BuildDetailAsync(conversation, await GetUnreadForCustomerAsync(conversation.Id, customerId, cancellationToken), cancellationToken);
        }

        public async Task<IReadOnlyList<ChatMessageDto>> GetCustomerMessagesAsync(long customerId, long conversationId, long? beforeMessageId, int take, CancellationToken cancellationToken = default)
        {
            var allowed = await _context.Conversations.AnyAsync(c => c.Id == conversationId && c.CustomerId == customerId, cancellationToken);
            if (!allowed) throw new UnauthorizedAccessException("Conversation access denied.");
            return await GetMessagesAsync(conversationId, beforeMessageId, take, cancellationToken);
        }

        public async Task<ConversationDetailDto> CreateCustomerConversationAsync(long customerId, ChatConversationCreateRequestDto request, CancellationToken cancellationToken = default)
        {
            request ??= new ChatConversationCreateRequestDto();
            var targetBranchId = request.BranchId > 0
                ? request.BranchId
                : await ResolveCustomerMessengerBranchIdAsync(customerId, null, cancellationToken);
            var now = DateTime.UtcNow;

            var existing = await _context.Conversations.FirstOrDefaultAsync(c =>
                c.CustomerId == customerId &&
                c.BranchId == targetBranchId &&
                c.AppointmentId == request.AppointmentId &&
                c.ServiceOrderId == request.ServiceOrderId &&
                c.VehicleId == request.VehicleId &&
                c.Status != nameof(ConversationStatus.CLOSED), cancellationToken);

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
                _context.Conversations.Add(existing);
                await SaveChangesWithConcurrencyRetryAsync(cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(request.InitialMessage))
            {
                await SendCustomerMessageAsync(customerId, existing.Id, new ChatSendMessageRequestDto { Content = request.InitialMessage }, cancellationToken);
            }

            var refreshed = await _context.Conversations.Include(c => c.Customer).FirstAsync(c => c.Id == existing.Id, cancellationToken);
            return await BuildDetailAsync(refreshed, await GetUnreadForCustomerAsync(refreshed.Id, customerId, cancellationToken), cancellationToken);
        }

        public async Task<ConversationDetailDto> GetOrCreateCustomerMessengerConversationAsync(long customerId, long? preferredBranchId, CancellationToken cancellationToken = default)
        {
            var conversation = await _context.Conversations
                .Where(c => c.CustomerId == customerId && c.Status != nameof(ConversationStatus.CLOSED))
                .OrderByDescending(c => c.LastMessageAt ?? c.UpdatedAt ?? c.CreatedAt)
                .ThenByDescending(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (conversation is null)
            {
                conversation = await _context.Conversations
                    .Where(c => c.CustomerId == customerId)
                    .OrderByDescending(c => c.LastMessageAt ?? c.UpdatedAt ?? c.CreatedAt)
                    .ThenByDescending(c => c.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }

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

                _context.Conversations.Add(conversation);
                await SaveChangesWithConcurrencyRetryAsync(cancellationToken);
            }

            var refreshed = await _context.Conversations
                .Include(c => c.Customer)
                .FirstAsync(c => c.Id == conversation.Id, cancellationToken);
            var unreadCount = await GetUnreadForCustomerAsync(refreshed.Id, customerId, cancellationToken);
            return await BuildDetailAsync(refreshed, unreadCount, cancellationToken);
        }

        public async Task<ChatMessageDto> SendCustomerMessageAsync(long customerId, long conversationId, ChatSendMessageRequestDto request, CancellationToken cancellationToken = default)
        {
            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId && c.CustomerId == customerId, cancellationToken)
                ?? throw new UnauthorizedAccessException("Conversation access denied.");
            return await SendMessageInternalAsync(conversation, customerId, nameof(ChatSenderType.CUSTOMER), nameof(ConversationStatus.WAITING_FOR_STAFF), request, cancellationToken);
        }

        public async Task<IReadOnlyList<ChatOrderCardDto>> GetCustomerConversationOrdersAsync(long customerId, long conversationId, CancellationToken cancellationToken = default)
        {
            var conversation = await _context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.CustomerId == customerId, cancellationToken);
            if (conversation is null)
            {
                throw new UnauthorizedAccessException("Conversation access denied.");
            }

            return await BuildConversationOrderCardsAsync(customerId, forReceptionist: false, cancellationToken);
        }

        public async Task MarkCustomerReadAsync(long customerId, long conversationId, long? lastReadMessageId, CancellationToken cancellationToken = default)
        {
            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId && c.CustomerId == customerId, cancellationToken)
                ?? throw new UnauthorizedAccessException("Conversation access denied.");
            var effectiveLastReadMessageId = await UpsertCustomerReadAsync(conversation.Id, customerId, lastReadMessageId, cancellationToken);
            await SaveChangesWithConcurrencyRetryAsync(cancellationToken);
            await NotifyReadStateUpdatedAsync(
                conversation,
                nameof(ChatSenderType.CUSTOMER),
                customerId,
                effectiveLastReadMessageId,
                cancellationToken);
        }

        public async Task<PagedConversationsResponseDto> GetReceptionConversationsAsync(long staffId, string? roleCode, long? branchId, string? status, bool? isUnread, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var targetBranch = ResolveReceptionistBranchScope(staff, branchId);
            var safePage = page <= 0 ? 1 : page;
            var safePageSize = pageSize <= 0 ? DefaultPageSize : pageSize;

            var query = _context.Conversations.AsNoTracking().AsQueryable();
            query = query.Where(c => c.BranchId == targetBranch);
            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToUpperInvariant();
                query = query.Where(c => c.Status == normalized);
            }

            var readQuery = _context.ConversationStaffReads
                .AsNoTracking()
                .Where(r => r.StaffId == staffId);

            var projected = from c in query
                            join r in readQuery on c.Id equals r.ConversationId into rg
                            from r in rg.DefaultIfEmpty()
                            let lastReadMessageId = r == null ? (long?)null : r.LastReadMessageId
                            select new
                            {
                                Conversation = c,
                                CustomerFullName = _context.Customers
                                    .Where(customer => customer.Id == c.CustomerId)
                                    .Select(customer => customer.FullName)
                                    .FirstOrDefault(),
                                UnreadCount = _context.ChatMessages.Count(m =>
                                    m.ConversationId == c.Id &&
                                    m.IsDeleted != true &&
                                    m.SenderType == nameof(ChatSenderType.CUSTOMER) &&
                                    m.Id > (lastReadMessageId ?? 0))
                            };

            if (isUnread.HasValue)
            {
                projected = isUnread.Value
                    ? projected.Where(x => x.UnreadCount > 0)
                    : projected.Where(x => x.UnreadCount == 0);
            }

            var rows = await projected
                .OrderByDescending(x => x.Conversation.LastMessageAt ?? x.Conversation.UpdatedAt ?? x.Conversation.CreatedAt)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .ToListAsync(cancellationToken);

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
                LastMessageAt = ToChatDisplayTime(x.Conversation.LastMessageAt ?? x.Conversation.UpdatedAt ?? x.Conversation.CreatedAt),
                UnreadCount = x.UnreadCount
            }).ToList();

            return new PagedConversationsResponseDto
            {
                Items = items,
                Page = safePage,
                PageSize = safePageSize
            };
        }

        public async Task<ConversationDetailDto?> GetReceptionConversationAsync(long staffId, string? roleCode, long conversationId, CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken);
            if (conversation is null) return null;
            return await BuildDetailAsync(conversation, await GetUnreadForStaffAsync(conversation.Id, staffId, cancellationToken), cancellationToken);
        }

        public async Task<IReadOnlyList<ChatMessageDto>> GetReceptionMessagesAsync(long staffId, string? roleCode, long conversationId, long? beforeMessageId, int take, CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken);
            if (conversation is null) throw new KeyNotFoundException("Conversation not found.");
            return await GetMessagesAsync(conversation.Id, beforeMessageId, take, cancellationToken);
        }

        public async Task<ChatMessageDto> SendStaffMessageAsync(long staffId, string? roleCode, long conversationId, ChatSendMessageRequestDto request, CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken)
                ?? throw new KeyNotFoundException("Conversation not found.");

            return await SendMessageInternalAsync(conversation, staffId, nameof(ChatSenderType.STAFF), nameof(ConversationStatus.WAITING_FOR_CUSTOMER), request, cancellationToken);
        }

        public async Task<IReadOnlyList<ChatOrderCardDto>> GetReceptionConversationOrdersAsync(long staffId, string? roleCode, long conversationId, CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken);
            if (conversation is null)
            {
                throw new KeyNotFoundException("Conversation not found.");
            }

            return await BuildConversationOrderCardsAsync(conversation.CustomerId, forReceptionist: true, cancellationToken);
        }

        public async Task<ConversationDetailDto> UpdateConversationStatusAsync(long staffId, string? roleCode, long conversationId, string status, string? reason, CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken)
                ?? throw new KeyNotFoundException("Conversation not found.");
            var normalized = NormalizeConversationStatus(status);
            var currentStatus = NormalizeStoredConversationStatus(conversation.Status);
            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (!CanTransitionByReceptionist(currentStatus, normalized))
            {
                throw new ChatConflictException($"Cannot change conversation status from {currentStatus} to {normalized}.");
            }

            if (currentStatus == normalized)
            {
                return await BuildDetailAsync(conversation, await GetUnreadForStaffAsync(conversation.Id, staffId, cancellationToken), cancellationToken);
            }

            conversation.Status = normalized;
            conversation.UpdatedAt = DateTime.UtcNow;
            AddChatAuditLog(
                actorId: staffId,
                actorType: nameof(ChatSenderType.STAFF),
                branchId: conversation.BranchId,
                action: "CHAT_STATUS_CHANGE",
                conversationId: conversation.Id,
                oldValue: new { status = currentStatus },
                newValue: new { status = normalized, reason = normalizedReason });

            await SaveChangesWithConcurrencyRetryAsync(cancellationToken);
            await NotifyConversationUpdatedAsync(conversation, cancellationToken);
            return await BuildDetailAsync(conversation, await GetUnreadForStaffAsync(conversation.Id, staffId, cancellationToken), cancellationToken);
        }

        public async Task MarkStaffReadAsync(long staffId, string? roleCode, long conversationId, long? lastReadMessageId, CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken)
                ?? throw new KeyNotFoundException("Conversation not found.");
            var effectiveLastReadMessageId = await UpsertStaffReadAsync(conversation.Id, staffId, lastReadMessageId, cancellationToken);
            await SaveChangesWithConcurrencyRetryAsync(cancellationToken);
            await NotifyReadStateUpdatedAsync(
                conversation,
                nameof(ChatSenderType.STAFF),
                staffId,
                effectiveLastReadMessageId,
                cancellationToken);
        }

        public async Task<ConversationDetailDto> AssignConversationAsync(long staffId, string? roleCode, long conversationId, long assignedStaffId, CancellationToken cancellationToken = default)
        {
            var staff = await RequireStaffAsync(staffId, roleCode, cancellationToken);
            var conversation = await GetConversationForStaffAsync(staff, conversationId, cancellationToken)
                ?? throw new KeyNotFoundException("Conversation not found.");

            var assignedStaff = await _context.Staffs
                .Include(s => s.Role)
                .FirstOrDefaultAsync(s => s.Id == assignedStaffId && s.IsDeleted != true, cancellationToken)
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
                return await BuildDetailAsync(conversation, await GetUnreadForStaffAsync(conversation.Id, staffId, cancellationToken), cancellationToken);
            }

            conversation.AssignedStaffId = assignedStaffId;
            conversation.UpdatedAt = DateTime.UtcNow;
            AddChatAuditLog(
                actorId: staffId,
                actorType: nameof(ChatSenderType.STAFF),
                branchId: conversation.BranchId,
                action: "CHAT_ASSIGN_RECEPTIONIST",
                conversationId: conversation.Id,
                oldValue: new { assignedStaffId = previousAssignedStaffId },
                newValue: new { assignedStaffId });

            await SaveChangesWithConcurrencyRetryAsync(cancellationToken);
            await NotifyConversationUpdatedAsync(conversation, cancellationToken);
            return await BuildDetailAsync(conversation, await GetUnreadForStaffAsync(conversation.Id, staffId, cancellationToken), cancellationToken);
        }

        private async Task<long> ResolveCustomerMessengerBranchIdAsync(long customerId, long? preferredBranchId, CancellationToken cancellationToken)
        {
            var branches = await _context.Branches
                .AsNoTracking()
                .Where(b => b.IsDeleted != true)
                .Select(b => new BranchCandidate
                {
                    Id = b.Id,
                    Name = b.Name,
                    Address = b.Address,
                    Status = b.Status
                })
                .ToListAsync(cancellationToken);

            if (branches.Count == 0)
            {
                throw new InvalidOperationException("No branch available to open messenger conversation.");
            }

            if (preferredBranchId.HasValue && branches.Any(b => b.Id == preferredBranchId.Value && IsBranchActive(b.Status)))
            {
                return preferredBranchId.Value;
            }

            var customerNote = await _context.Customers
                .AsNoTracking()
                .Where(c => c.Id == customerId && c.IsDeleted != true)
                .Select(c => c.Note)
                .FirstOrDefaultAsync(cancellationToken);

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

        private async Task<IReadOnlyList<ChatOrderCardDto>> BuildConversationOrderCardsAsync(
            long customerId,
            bool forReceptionist,
            CancellationToken cancellationToken)
        {
            var retailCards = await BuildRetailOrderCardsAsync(customerId, forReceptionist, cancellationToken);
            var serviceCards = await BuildServiceOrderCardsAsync(customerId, forReceptionist, cancellationToken);

            return retailCards
                .Concat(serviceCards)
                .OrderByDescending(card => card.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(card => card.OrderId)
                .Take(MaxOrderCardsPerConversation)
                .ToList();
        }

        private async Task<IReadOnlyList<ChatOrderCardDto>> BuildRetailOrderCardsAsync(
            long customerId,
            bool forReceptionist,
            CancellationToken cancellationToken)
        {
            var orders = await _context.RetailOrders
                .AsNoTracking()
                .Include(order => order.Branch)
                .Include(order => order.RetailOrderItems)
                    .ThenInclude(item => item.Product)
                        .ThenInclude(product => product.ProductImages)
                .Where(order => order.CustomerId == customerId)
                .OrderByDescending(order => order.CreatedAt ?? order.UpdatedAt ?? DateTime.MinValue)
                .ThenByDescending(order => order.Id)
                .Take(MaxOrderCardsPerConversation)
                .ToListAsync(cancellationToken);

            var cards = new List<ChatOrderCardDto>(orders.Count);
            foreach (var order in orders)
            {
                if (!IsRetailOrderPurchased(order.Status))
                {
                    continue;
                }

                var details = order.RetailOrderItems
                    .OrderBy(item => item.Id)
                    .ToList();
                if (details.Count == 0)
                {
                    continue;
                }

                var rawItemCount = details.Sum(item => item.Quantity);
                var itemCount = rawItemCount <= 0
                    ? 1
                    : (int)Math.Max(
                        1,
                        Math.Min(int.MaxValue, Math.Round((double)rawItemCount, MidpointRounding.AwayFromZero)));

                var primaryItem = details
                    .OrderByDescending(item => item.FinalAmount)
                    .ThenBy(item => item.Id)
                    .FirstOrDefault();

                var imageUrl = primaryItem?.Product?.ProductImages?
                    .OrderByDescending(image => image.IsMain == true)
                    .ThenBy(image => image.SortOrder ?? int.MaxValue)
                    .ThenBy(image => image.Id)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault();

                var status = NormalizeToken(order.Status);
                cards.Add(new ChatOrderCardDto
                {
                    OrderType = "RETAIL",
                    OrderId = order.Id,
                    OrderCode = string.IsNullOrWhiteSpace(order.OrderCode) ? $"RO-{order.Id}" : order.OrderCode.Trim(),
                    Title = SafeText(primaryItem?.Product?.Name, $"Don phu tung #{order.Id}"),
                    Subtitle = $"{itemCount} san pham, Tong don: {FormatCurrencyVnd(order.FinalAmount)}",
                    Status = status,
                    StatusLabel = ResolveOrderStatusLabel("RETAIL", status),
                    TotalAmount = order.FinalAmount,
                    ItemCount = itemCount,
                    ImageUrl = imageUrl,
                    BranchName = order.Branch?.Name,
                    CreatedAt = ToChatDisplayTime(order.CreatedAt ?? order.UpdatedAt),
                    DetailUrl = forReceptionist ? null : $"/my-retail-orders?orderId={order.Id}"
                });
            }

            return cards;
        }

        private async Task<IReadOnlyList<ChatOrderCardDto>> BuildServiceOrderCardsAsync(
            long customerId,
            bool forReceptionist,
            CancellationToken cancellationToken)
        {
            var orders = await _context.ServiceOrders
                .AsNoTracking()
                .Include(order => order.ExecuteBranch)
                .Include(order => order.ServiceOrderDetails)
                .Where(order => order.CustomerId == customerId)
                .OrderByDescending(order => order.CreatedAt ?? order.UpdatedAt ?? DateTime.MinValue)
                .ThenByDescending(order => order.Id)
                .Take(MaxOrderCardsPerConversation)
                .ToListAsync(cancellationToken);

            var productIds = orders
                .SelectMany(order => order.ServiceOrderDetails ?? Array.Empty<ServiceOrderDetail>())
                .Where(detail => IsServiceDetailProduct(detail) && detail.ItemId.HasValue)
                .Select(detail => detail.ItemId!.Value)
                .Distinct()
                .ToList();

            Dictionary<long, string?> imageByProductId = new();
            if (productIds.Count > 0)
            {
                var images = await _context.ProductImages
                    .AsNoTracking()
                    .Where(image => productIds.Contains(image.ProductId))
                    .ToListAsync(cancellationToken);

                imageByProductId = images
                    .GroupBy(image => image.ProductId)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .OrderByDescending(image => image.IsMain == true)
                            .ThenBy(image => image.SortOrder ?? int.MaxValue)
                            .ThenBy(image => image.Id)
                            .Select(image => image.ImageUrl)
                            .FirstOrDefault());
            }

            var cards = new List<ChatOrderCardDto>(orders.Count);
            foreach (var order in orders)
            {
                if (!IsServiceOrderDisplayable(order.Status))
                {
                    continue;
                }

                var details = order.ServiceOrderDetails
                    .OrderBy(detail => detail.Id)
                    .ToList();
                if (details.Count == 0)
                {
                    continue;
                }

                var primaryDetail = details
                    .OrderByDescending(detail => detail.FinalAmount)
                    .ThenBy(detail => detail.Id)
                    .First();

                string? imageUrl = null;
                if (IsServiceDetailProduct(primaryDetail)
                    && primaryDetail.ItemId.HasValue
                    && imageByProductId.TryGetValue(primaryDetail.ItemId.Value, out var mappedImage))
                {
                    imageUrl = mappedImage;
                }

                var totalAmount = order.FinalAmount ?? details.Sum(detail => detail.FinalAmount);
                var itemCount = Math.Max(1, details.Count);
                var status = NormalizeToken(order.Status);
                cards.Add(new ChatOrderCardDto
                {
                    OrderType = "SERVICE",
                    OrderId = order.Id,
                    OrderCode = string.IsNullOrWhiteSpace(order.Code) ? $"SO-{order.Id}" : order.Code.Trim(),
                    Title = SafeText(primaryDetail.ItemName, $"Don dich vu #{order.Id}"),
                    Subtitle = $"{itemCount} hang muc, Tong don: {FormatCurrencyVnd(totalAmount)}",
                    Status = status,
                    StatusLabel = ResolveOrderStatusLabel("SERVICE", status),
                    TotalAmount = totalAmount,
                    ItemCount = itemCount,
                    ImageUrl = imageUrl,
                    BranchName = order.ExecuteBranch?.Name,
                    CreatedAt = ToChatDisplayTime(order.CreatedAt ?? order.UpdatedAt),
                    DetailUrl = forReceptionist
                        ? $"/workshop/reception?viewOrderId={order.Id}"
                        : order.AppointmentId.HasValue
                            ? $"/my-appointments?appointmentId={order.AppointmentId.Value}"
                            : "/my-appointments"
                });
            }

            return cards;
        }

        private static bool IsRetailOrderPurchased(string? status)
        {
            var normalized = NormalizeToken(status);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return true;
            }

            return normalized != "DRAFT"
                && normalized != "CANCELLED"
                && normalized != "CANCELED";
        }

        private static bool IsServiceOrderDisplayable(string? status)
        {
            var normalized = NormalizeToken(status);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            return normalized == "READY"
                || normalized == "CLOSED"
                || normalized == "COMPLETED"
                || normalized == "WAITING_PAYMENT";
        }

        private static bool IsServiceDetailProduct(ServiceOrderDetail detail)
        {
            var type = NormalizeToken(detail.ItemType);
            return type == "PRODUCT"
                || type == "PART"
                || type == "PHUTUNG"
                || type == "PHU_TUNG";
        }

        private static string ResolveOrderStatusLabel(string orderType, string status)
        {
            var normalizedType = NormalizeToken(orderType);
            var normalizedStatus = NormalizeToken(status);

            if (normalizedType == "RETAIL")
            {
                return normalizedStatus switch
                {
                    "CONFIRMED" => "Da xac nhan",
                    "PAID" => "Da thanh toan",
                    "COMPLETED" => "Da hoan thanh",
                    "DRAFT" => "Cho thanh toan",
                    _ => string.IsNullOrWhiteSpace(normalizedStatus) ? "Dang xu ly" : normalizedStatus
                };
            }

            return normalizedStatus switch
            {
                "READY" => "San sang ban giao",
                "COMPLETED" => "Da hoan thanh",
                "CLOSED" => "Da dong",
                "WAITING_PAYMENT" => "Cho thanh toan",
                "REPAIRING" => "Dang sua chua",
                "WAITING_APPROVAL" => "Cho bao gia",
                _ => string.IsNullOrWhiteSpace(normalizedStatus) ? "Dang xu ly" : normalizedStatus
            };
        }

        private static string FormatCurrencyVnd(decimal? amount)
        {
            var safe = amount ?? 0m;
            var text = safe.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
            return $"{text}d";
        }

        private static string NormalizeToken(string? value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string SafeText(string? value, string fallback)
        {
            var normalized = (value ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        private static BranchCandidate? FindBranchByRegion(IReadOnlyCollection<BranchCandidate> branches, string region)
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

        private static BranchCandidate? FindNorthFallbackBranch(IReadOnlyCollection<BranchCandidate> branches)
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

        private async Task<ChatMessageDto> SendMessageInternalAsync(Conversation conversation, long senderId, string senderType, string targetStatus, ChatSendMessageRequestDto request, CancellationToken cancellationToken)
        {
            request ??= new ChatSendMessageRequestDto();
            var hasOrderCard = request.OrderCard is not null;
            var normalizedOrderCard = hasOrderCard ? NormalizeOrderCardRequest(request.OrderCard!) : null;
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
                var dup = await _context.ChatMessages.FirstOrDefaultAsync(m => m.ClientMessageId == request.ClientMessageId.Value, cancellationToken);
                if (dup is not null)
                {
                    if (dup.ConversationId != conversation.Id)
                    {
                        throw new ChatConflictException("ClientMessageId already exists in another conversation.");
                    }

                    var existingAttachmentLookup = await GetAttachmentLookupAsync(new[] { dup.Id }, cancellationToken);
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
                ? JsonSerializer.Serialize(ToStoredOrderCardPayload(normalizedOrderCard), OrderCardSerializerOptions)
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

            _context.ChatMessages.Add(message);
            conversation.Status = nextStatus;
            conversation.LastMessage = normalizedOrderCard is not null
                ? BuildOrderCardConversationPreview(normalizedOrderCard)
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
                    actorId: senderId,
                    actorType: nameof(ChatSenderType.CUSTOMER),
                    branchId: conversation.BranchId,
                    action: "CHAT_REOPEN_BY_CUSTOMER",
                    conversationId: conversation.Id,
                    oldValue: new { status = currentStatus },
                    newValue: new { status = nextStatus });
            }

            await SaveChangesWithConcurrencyRetryAsync(cancellationToken);

            conversation.LastMessageId = message.Id;
            await SaveChangesWithConcurrencyRetryAsync(cancellationToken);

            var dto = MapMessage(message, BuildAttachmentDtosFromEntity(message.ChatMessageAttachments));
            await _hubContext.Clients.Group($"Conversation_{conversation.Id}").SendAsync("ReceiveMessage", dto, cancellationToken);
            if (conversation.BranchId.HasValue)
            {
                await _hubContext.Clients.Group($"BranchInbox_{conversation.BranchId.Value}")
                    .SendAsync("ConversationUpdated", new { conversationId = conversation.Id }, cancellationToken);
            }
            return dto;
        }

        private async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(long conversationId, long? beforeMessageId, int take, CancellationToken cancellationToken)
        {
            var safeTake = take <= 0 ? DefaultMessageTake : take;
            var query = _context.ChatMessages.AsNoTracking().Where(m => m.ConversationId == conversationId && m.IsDeleted != true);
            if (beforeMessageId.HasValue) query = query.Where(m => m.Id < beforeMessageId.Value);
            var list = await query.OrderByDescending(m => m.Id).Take(safeTake).ToListAsync(cancellationToken);
            list.Reverse();
            if (list.Count == 0)
            {
                return Array.Empty<ChatMessageDto>();
            }

            var attachmentLookup = await GetAttachmentLookupAsync(list.Select(x => x.Id).ToArray(), cancellationToken);
            var result = new List<ChatMessageDto>(list.Count);
            foreach (var message in list)
            {
                attachmentLookup.TryGetValue(message.Id, out var attachments);
                result.Add(MapMessage(message, attachments));
            }

            return result;
        }

        private async Task<long?> UpsertCustomerReadAsync(long conversationId, long customerId, long? lastReadMessageId, CancellationToken cancellationToken)
        {
            var target = await ResolveValidLastReadMessageIdAsync(conversationId, lastReadMessageId, cancellationToken);
            if (!target.HasValue) return null;
            var now = DateTime.UtcNow;
            var read = await _context.ConversationCustomerReads.FirstOrDefaultAsync(r => r.ConversationId == conversationId, cancellationToken);
            if (read is null)
            {
                _context.ConversationCustomerReads.Add(new ConversationCustomerRead { ConversationId = conversationId, CustomerId = customerId, LastReadMessageId = target, LastReadAt = now, UpdatedAt = now });
                return target;
            }
            if (!read.LastReadMessageId.HasValue || target.Value > read.LastReadMessageId.Value) read.LastReadMessageId = target.Value;
            read.LastReadAt = now;
            read.UpdatedAt = now;
            return read.LastReadMessageId;
        }

        private async Task<long?> UpsertStaffReadAsync(long conversationId, long staffId, long? lastReadMessageId, CancellationToken cancellationToken)
        {
            var target = await ResolveValidLastReadMessageIdAsync(conversationId, lastReadMessageId, cancellationToken);
            if (!target.HasValue) return null;
            var now = DateTime.UtcNow;
            var read = await _context.ConversationStaffReads.FirstOrDefaultAsync(r => r.ConversationId == conversationId && r.StaffId == staffId, cancellationToken);
            if (read is null)
            {
                _context.ConversationStaffReads.Add(new ConversationStaffRead { ConversationId = conversationId, StaffId = staffId, LastReadMessageId = target, LastReadAt = now, UpdatedAt = now });
                return target;
            }
            if (!read.LastReadMessageId.HasValue || target.Value > read.LastReadMessageId.Value) read.LastReadMessageId = target.Value;
            read.LastReadAt = now;
            read.UpdatedAt = now;
            return read.LastReadMessageId;
        }

        private async Task<long?> ResolveValidLastReadMessageIdAsync(long conversationId, long? requestedLastReadMessageId, CancellationToken cancellationToken)
        {
            if (!requestedLastReadMessageId.HasValue)
            {
                return await _context.ChatMessages
                    .Where(m => m.ConversationId == conversationId && m.IsDeleted != true)
                    .MaxAsync(m => (long?)m.Id, cancellationToken);
            }

            var exists = await _context.ChatMessages
                .AsNoTracking()
                .AnyAsync(
                    m => m.Id == requestedLastReadMessageId.Value &&
                         m.ConversationId == conversationId &&
                         m.IsDeleted != true,
                    cancellationToken);

            if (!exists)
            {
                throw new ChatConflictException("LastReadMessageId is invalid for this conversation.");
            }

            return requestedLastReadMessageId.Value;
        }

        private async Task<int> GetUnreadForCustomerAsync(long conversationId, long customerId, CancellationToken cancellationToken)
        {
            var lastRead = await _context.ConversationCustomerReads.Where(r => r.ConversationId == conversationId && r.CustomerId == customerId).Select(r => (long?)r.LastReadMessageId).FirstOrDefaultAsync(cancellationToken) ?? 0;
            return await _context.ChatMessages.CountAsync(m => m.ConversationId == conversationId && m.IsDeleted != true && m.SenderType != nameof(ChatSenderType.CUSTOMER) && m.Id > lastRead, cancellationToken);
        }

        private async Task<int> GetUnreadForStaffAsync(long conversationId, long staffId, CancellationToken cancellationToken)
        {
            var lastRead = await _context.ConversationStaffReads.Where(r => r.ConversationId == conversationId && r.StaffId == staffId).Select(r => (long?)r.LastReadMessageId).FirstOrDefaultAsync(cancellationToken) ?? 0;
            return await _context.ChatMessages.CountAsync(m => m.ConversationId == conversationId && m.IsDeleted != true && m.SenderType == nameof(ChatSenderType.CUSTOMER) && m.Id > lastRead, cancellationToken);
        }

        private async Task<Conversation?> GetConversationForStaffAsync(Staff staff, long conversationId, CancellationToken cancellationToken)
        {
            var c = await _context.Conversations.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);
            if (c is null) return null;
            if (!staff.BranchId.HasValue || !c.BranchId.HasValue || c.BranchId.Value != staff.BranchId.Value)
                throw new UnauthorizedAccessException("Conversation is outside your branch.");
            return c;
        }

        private async Task<Staff> RequireStaffAsync(long staffId, string? roleCode, CancellationToken cancellationToken)
        {
            var staff = await _context.Staffs
                .Include(s => s.Role)
                .FirstOrDefaultAsync(s => s.Id == staffId && s.IsDeleted != true, cancellationToken);
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

        private async Task<ConversationDetailDto> BuildDetailAsync(Conversation c, int unreadCount, CancellationToken cancellationToken)
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
                CustomerName = c.Customer?.FullName,
                CustomerPhone = c.Customer?.Phone,
                Messages = await GetMessagesAsync(c.Id, null, DefaultMessageTake, cancellationToken)
            };
        }

        private static ChatOrderCardDto NormalizeOrderCardRequest(ChatOrderCardRequestDto request)
        {
            if (request.OrderId <= 0)
            {
                throw new InvalidOperationException("OrderCard.OrderId must be greater than 0.");
            }

            var orderType = NormalizeToken(request.OrderType);
            if (orderType != "RETAIL" && orderType != "SERVICE")
            {
                throw new InvalidOperationException("OrderCard.OrderType must be RETAIL or SERVICE.");
            }

            var orderCode = SafeText(request.OrderCode, orderType == "RETAIL" ? $"RO-{request.OrderId}" : $"SO-{request.OrderId}");
            var title = SafeText(request.Title, orderCode);
            var subtitle = SafeText(request.Subtitle, "Thong tin don hang");
            var status = NormalizeToken(request.Status);
            var statusLabel = SafeText(request.StatusLabel, ResolveOrderStatusLabel(orderType, status));
            var normalizedDetailUrl = NormalizeDetailUrl(request.DetailUrl);

            return new ChatOrderCardDto
            {
                OrderType = orderType,
                OrderId = request.OrderId,
                OrderCode = orderCode,
                Title = title,
                Subtitle = subtitle,
                Status = status,
                StatusLabel = statusLabel,
                TotalAmount = request.TotalAmount,
                ItemCount = request.ItemCount,
                ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
                BranchName = string.IsNullOrWhiteSpace(request.BranchName) ? null : request.BranchName.Trim(),
                CreatedAt = request.CreatedAt,
                DetailUrl = normalizedDetailUrl
            };
        }

        private static StoredOrderCardPayload ToStoredOrderCardPayload(ChatOrderCardDto orderCard)
        {
            return new StoredOrderCardPayload
            {
                OrderType = orderCard.OrderType,
                OrderId = orderCard.OrderId,
                OrderCode = orderCard.OrderCode,
                Title = orderCard.Title,
                Subtitle = orderCard.Subtitle,
                Status = orderCard.Status,
                StatusLabel = orderCard.StatusLabel,
                TotalAmount = orderCard.TotalAmount,
                ItemCount = orderCard.ItemCount,
                ImageUrl = orderCard.ImageUrl,
                BranchName = orderCard.BranchName,
                CreatedAt = orderCard.CreatedAt,
                DetailUrl = orderCard.DetailUrl
            };
        }

        private static string BuildOrderCardConversationPreview(ChatOrderCardDto orderCard)
        {
            var title = SafeText(orderCard.Title, orderCard.OrderCode);
            return $"[Don hang] {title}";
        }

        private static string? NormalizeDetailUrl(string? detailUrl)
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

        private async Task<Dictionary<long, IReadOnlyList<ChatMessageAttachmentDto>>> GetAttachmentLookupAsync(IReadOnlyCollection<long> messageIds, CancellationToken cancellationToken)
        {
            if (messageIds.Count == 0)
            {
                return new Dictionary<long, IReadOnlyList<ChatMessageAttachmentDto>>();
            }

            var attachments = await _context.ChatMessageAttachments
                .AsNoTracking()
                .Where(x => messageIds.Contains(x.MessageId))
                .OrderBy(x => x.MessageId)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

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

        private static bool IsReceptionistRole(params string?[] values)
        {
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var normalized = value.Trim().ToUpperInvariant();
                var noSpace = normalized.Replace(" ", string.Empty);

                if (normalized == "RECEPTIONIST"
                    || normalized.Contains("RECEPTION", StringComparison.OrdinalIgnoreCase)
                    || noSpace.Contains("LETAN", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeConversationStatus(string? status)
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

        private static string NormalizeStoredConversationStatus(string? status)
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

        private void AddChatAuditLog(long actorId, string actorType, long? branchId, string action, long conversationId, object? oldValue, object? newValue)
        {
            _context.AuditLogs.Add(new AuditLog
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

        private async Task SaveChangesWithConcurrencyRetryAsync(CancellationToken cancellationToken)
        {
            const int maxRetries = 2;
            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    return;
                }
                catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries)
                {
                    foreach (var entry in ex.Entries)
                    {
                        var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
                        if (databaseValues is null)
                        {
                            throw new ChatConflictException("Conversation no longer exists.");
                        }

                        entry.OriginalValues.SetValues(databaseValues);
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw new ChatConflictException("Conversation was updated by another request. Please refresh and try again.");
                }
            }
        }

        private async Task NotifyReadStateUpdatedAsync(Conversation conversation, string readerType, long readerId, long? lastReadMessageId, CancellationToken cancellationToken)
        {
            await _hubContext.Clients.Group($"Conversation_{conversation.Id}")
                .SendAsync(
                    "ReadStateUpdated",
                    new
                    {
                        conversationId = conversation.Id,
                        readerType,
                        readerId,
                        lastReadMessageId
                    },
                    cancellationToken);

            if (conversation.BranchId.HasValue)
            {
                await _hubContext.Clients.Group($"BranchInbox_{conversation.BranchId.Value}")
                    .SendAsync("ConversationUpdated", new { conversationId = conversation.Id }, cancellationToken);
            }
        }

        private async Task NotifyConversationUpdatedAsync(Conversation conversation, CancellationToken cancellationToken)
        {
            await _hubContext.Clients.Group($"Conversation_{conversation.Id}")
                .SendAsync("ConversationUpdated", new { conversationId = conversation.Id }, cancellationToken);

            if (conversation.BranchId.HasValue)
            {
                await _hubContext.Clients.Group($"BranchInbox_{conversation.BranchId.Value}")
                    .SendAsync("ConversationUpdated", new { conversationId = conversation.Id }, cancellationToken);
            }
        }

        private sealed class BranchCandidate
        {
            public long Id { get; init; }
            public string? Name { get; init; }
            public string? Address { get; init; }
            public string? Status { get; init; }
        }

        private static ChatMessageDto MapMessage(ChatMessage m, IReadOnlyList<ChatMessageAttachmentDto>? attachments = null)
        {
            var resolvedAttachments = attachments ?? BuildAttachmentDtosFromEntity(m.ChatMessageAttachments);
            var fallbackAttachmentUrl = resolvedAttachments.FirstOrDefault()?.AttachmentUrl;
            var messageType = NormalizeMessageType(m.MessageType);
            var orderCard = TryParseStoredOrderCard(messageType, m.Content);
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

        private static ChatOrderCardDto? TryParseStoredOrderCard(string messageType, string? content)
        {
            var isOrderCardType = string.Equals(
                messageType,
                nameof(ChatMessageType.ORDER_CARD),
                StringComparison.OrdinalIgnoreCase);

            if (!isOrderCardType
                && (
                    string.IsNullOrWhiteSpace(content)
                    || content.IndexOf("orderType", StringComparison.OrdinalIgnoreCase) < 0
                    || content.IndexOf("orderId", StringComparison.OrdinalIgnoreCase) < 0
                ))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<StoredOrderCardPayload>(content, OrderCardSerializerOptions);
                if (parsed is null || parsed.OrderId <= 0)
                {
                    return null;
                }

                var orderType = NormalizeToken(parsed.OrderType);
                if (orderType != "RETAIL" && orderType != "SERVICE")
                {
                    return null;
                }

                var normalizedStatus = NormalizeToken(parsed.Status);
                return new ChatOrderCardDto
                {
                    OrderType = orderType,
                    OrderId = parsed.OrderId,
                    OrderCode = SafeText(parsed.OrderCode, orderType == "RETAIL" ? $"RO-{parsed.OrderId}" : $"SO-{parsed.OrderId}"),
                    Title = SafeText(parsed.Title, "Don hang"),
                    Subtitle = SafeText(parsed.Subtitle, "Thong tin don hang"),
                    Status = normalizedStatus,
                    StatusLabel = SafeText(parsed.StatusLabel, ResolveOrderStatusLabel(orderType, normalizedStatus)),
                    TotalAmount = parsed.TotalAmount,
                    ItemCount = parsed.ItemCount,
                    ImageUrl = string.IsNullOrWhiteSpace(parsed.ImageUrl) ? null : parsed.ImageUrl.Trim(),
                    BranchName = string.IsNullOrWhiteSpace(parsed.BranchName) ? null : parsed.BranchName.Trim(),
                    CreatedAt = parsed.CreatedAt,
                    DetailUrl = NormalizeDetailUrl(parsed.DetailUrl)
                };
            }
            catch
            {
                return null;
            }
        }

        private static DateTime? ToChatDisplayTime(DateTime? value)
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

        private sealed class StoredOrderCardPayload
        {
            public string? OrderType { get; set; }
            public long OrderId { get; set; }
            public string? OrderCode { get; set; }
            public string? Title { get; set; }
            public string? Subtitle { get; set; }
            public string? Status { get; set; }
            public string? StatusLabel { get; set; }
            public decimal? TotalAmount { get; set; }
            public int? ItemCount { get; set; }
            public string? ImageUrl { get; set; }
            public string? BranchName { get; set; }
            public DateTime? CreatedAt { get; set; }
            public string? DetailUrl { get; set; }
        }
    }
}
