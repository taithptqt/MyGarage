using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Chat;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.DTOs.Responses.Chat;
using MyGarage.Exceptions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Chat;

public sealed class EfChatStore : IChatStore
{
    private readonly IGarageContext _context;

    public EfChatStore(IGarageContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Conversation>> GetCustomerConversationsPageAsync(
        long customerId,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Conversations
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(c => c.Status == normalizedStatus);
        }

        return await query
            .OrderByDescending(c => c.LastMessageAt ?? c.UpdatedAt ?? c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<Conversation?> GetCustomerConversationWithCustomerAsync(
        long customerId,
        long conversationId,
        CancellationToken ct = default)
    {
        return _context.Conversations
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.CustomerId == customerId, ct);
    }

    public Task<bool> CustomerOwnsConversationAsync(
        long customerId,
        long conversationId,
        CancellationToken ct = default)
    {
        return _context.Conversations
            .AnyAsync(c => c.Id == conversationId && c.CustomerId == customerId, ct);
    }

    public Task<Conversation?> FindOpenCustomerConversationAsync(
        long customerId,
        long branchId,
        long? appointmentId,
        long? serviceOrderId,
        long? vehicleId,
        CancellationToken ct = default)
    {
        return _context.Conversations.FirstOrDefaultAsync(c =>
            c.CustomerId == customerId &&
            c.BranchId == branchId &&
            c.AppointmentId == appointmentId &&
            c.ServiceOrderId == serviceOrderId &&
            c.VehicleId == vehicleId &&
            c.Status != nameof(ConversationStatus.CLOSED), ct);
    }

    public Task<Conversation?> GetLatestCustomerConversationAsync(
        long customerId,
        bool excludeClosed,
        CancellationToken ct = default)
    {
        var query = _context.Conversations.Where(c => c.CustomerId == customerId);
        if (excludeClosed)
        {
            query = query.Where(c => c.Status != nameof(ConversationStatus.CLOSED));
        }

        return query
            .OrderByDescending(c => c.LastMessageAt ?? c.UpdatedAt ?? c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<Conversation?> GetCustomerConversationForUpdateAsync(
        long customerId,
        long conversationId,
        CancellationToken ct = default)
    {
        return _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.CustomerId == customerId, ct);
    }

    public Task<Conversation?> GetConversationWithCustomerByIdAsync(
        long conversationId,
        bool track = true,
        CancellationToken ct = default)
    {
        var query = _context.Conversations.Include(c => c.Customer).AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(c => c.Id == conversationId, ct);
    }

    public void AddConversation(Conversation conversation)
    {
        _context.Conversations.Add(conversation);
    }

    public async Task<IReadOnlyList<ReceptionConversationRow>> GetReceptionConversationsPageAsync(
        long staffId,
        long targetBranchId,
        string? normalizedStatus,
        bool? isUnread,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Conversations
            .AsNoTracking()
            .Where(c => c.BranchId == targetBranchId);

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(c => c.Status == normalizedStatus);
        }

        var readQuery = _context.ConversationStaffReads
            .AsNoTracking()
            .Where(r => r.StaffId == staffId);

        var projected = from c in query
                        join r in readQuery on c.Id equals r.ConversationId into rg
                        from r in rg.DefaultIfEmpty()
                        let lastReadMessageId = r == null ? (long?)null : r.LastReadMessageId
                        select new ReceptionConversationRow
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

        return await projected
            .OrderByDescending(x => x.Conversation.LastMessageAt ?? x.Conversation.UpdatedAt ?? x.Conversation.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<Staff?> GetActiveStaffWithRoleAsync(long staffId, CancellationToken ct = default)
    {
        return _context.Staffs
            .Include(s => s.Role)
            .FirstOrDefaultAsync(s => s.Id == staffId && s.IsDeleted != true, ct);
    }

    public async Task<IReadOnlyList<ChatBranchCandidate>> GetActiveChatBranchesAsync(CancellationToken ct = default)
    {
        return await _context.Branches
            .AsNoTracking()
            .Where(b => b.IsDeleted != true)
            .Select(b => new ChatBranchCandidate
            {
                Id = b.Id,
                Name = b.Name,
                Address = b.Address,
                Status = b.Status
            })
            .ToListAsync(ct);
    }

    public Task<string?> GetCustomerNoteAsync(long customerId, CancellationToken ct = default)
    {
        return _context.Customers
            .AsNoTracking()
            .Where(c => c.Id == customerId && c.IsDeleted != true)
            .Select(c => c.Note)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<RetailOrder>> GetCustomerRetailOrdersForChatAsync(
        long customerId,
        int maxTake,
        CancellationToken ct = default)
    {
        return await _context.RetailOrders
            .AsNoTracking()
            .Include(order => order.Branch)
            .Include(order => order.RetailOrderItems)
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.ProductImages)
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAt ?? order.UpdatedAt ?? DateTime.MinValue)
            .ThenByDescending(order => order.Id)
            .Take(maxTake)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ServiceOrder>> GetCustomerServiceOrdersForChatAsync(
        long customerId,
        int maxTake,
        CancellationToken ct = default)
    {
        return await _context.ServiceOrders
            .AsNoTracking()
            .Include(order => order.ExecuteBranch)
            .Include(order => order.ServiceOrderDetails)
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAt ?? order.UpdatedAt ?? DateTime.MinValue)
            .ThenByDescending(order => order.Id)
            .Take(maxTake)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductImage>> GetProductImagesByProductIdsAsync(
        IReadOnlyCollection<long> productIds,
        CancellationToken ct = default)
    {
        if (productIds.Count == 0)
        {
            return Array.Empty<ProductImage>();
        }

        return await _context.ProductImages
            .AsNoTracking()
            .Where(image => productIds.Contains(image.ProductId))
            .ToListAsync(ct);
    }

    public Task<ChatMessage?> GetChatMessageByClientMessageIdAsync(Guid clientMessageId, CancellationToken ct = default)
    {
        return _context.ChatMessages.FirstOrDefaultAsync(m => m.ClientMessageId == clientMessageId, ct);
    }

    public void AddChatMessage(ChatMessage message)
    {
        _context.ChatMessages.Add(message);
    }

    public async Task<List<ChatMessage>> GetConversationMessagesAsync(
        long conversationId,
        long? beforeMessageId,
        int take,
        CancellationToken ct = default)
    {
        var query = _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.IsDeleted != true);

        if (beforeMessageId.HasValue)
        {
            query = query.Where(m => m.Id < beforeMessageId.Value);
        }

        return await query
            .OrderByDescending(m => m.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<long?> GetMaxChatMessageIdAsync(long conversationId, CancellationToken ct = default)
    {
        return _context.ChatMessages
            .Where(m => m.ConversationId == conversationId && m.IsDeleted != true)
            .MaxAsync(m => (long?)m.Id, ct);
    }

    public Task<bool> ChatMessageExistsInConversationAsync(
        long messageId,
        long conversationId,
        CancellationToken ct = default)
    {
        return _context.ChatMessages
            .AsNoTracking()
            .AnyAsync(
                m => m.Id == messageId &&
                     m.ConversationId == conversationId &&
                     m.IsDeleted != true,
                ct);
    }

    public Task<ConversationCustomerRead?> GetCustomerReadAsync(long conversationId, CancellationToken ct = default)
    {
        return _context.ConversationCustomerReads
            .FirstOrDefaultAsync(r => r.ConversationId == conversationId, ct);
    }

    public void AddCustomerRead(ConversationCustomerRead read)
    {
        _context.ConversationCustomerReads.Add(read);
    }

    public Task<ConversationStaffRead?> GetStaffReadAsync(
        long conversationId,
        long staffId,
        CancellationToken ct = default)
    {
        return _context.ConversationStaffReads
            .FirstOrDefaultAsync(r => r.ConversationId == conversationId && r.StaffId == staffId, ct);
    }

    public void AddStaffRead(ConversationStaffRead read)
    {
        _context.ConversationStaffReads.Add(read);
    }

    public async Task<int> GetUnreadForCustomerAsync(
        long conversationId,
        long customerId,
        CancellationToken ct = default)
    {
        var lastRead = await _context.ConversationCustomerReads
            .Where(r => r.ConversationId == conversationId && r.CustomerId == customerId)
            .Select(r => (long?)r.LastReadMessageId)
            .FirstOrDefaultAsync(ct) ?? 0;

        return await _context.ChatMessages.CountAsync(
            m => m.ConversationId == conversationId &&
                 m.IsDeleted != true &&
                 m.SenderType != nameof(ChatSenderType.CUSTOMER) &&
                 m.Id > lastRead,
            ct);
    }

    public async Task<int> GetUnreadForStaffAsync(
        long conversationId,
        long staffId,
        CancellationToken ct = default)
    {
        var lastRead = await _context.ConversationStaffReads
            .Where(r => r.ConversationId == conversationId && r.StaffId == staffId)
            .Select(r => (long?)r.LastReadMessageId)
            .FirstOrDefaultAsync(ct) ?? 0;

        return await _context.ChatMessages.CountAsync(
            m => m.ConversationId == conversationId &&
                 m.IsDeleted != true &&
                 m.SenderType == nameof(ChatSenderType.CUSTOMER) &&
                 m.Id > lastRead,
            ct);
    }

    public async Task<IReadOnlyList<ChatMessageAttachment>> GetAttachmentsByMessageIdsAsync(
        IReadOnlyCollection<long> messageIds,
        CancellationToken ct = default)
    {
        if (messageIds.Count == 0)
        {
            return Array.Empty<ChatMessageAttachment>();
        }

        return await _context.ChatMessageAttachments
            .AsNoTracking()
            .Where(x => messageIds.Contains(x.MessageId))
            .OrderBy(x => x.MessageId)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
    }

    public void AddAuditLog(AuditLog auditLog)
    {
        _context.AuditLogs.Add(auditLog);
    }

    public async Task SaveChangesWithConcurrencyRetryAsync(CancellationToken ct = default)
    {
        const int maxRetries = 2;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _context.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries)
            {
                foreach (var entry in ex.Entries)
                {
                    var databaseValues = await entry.GetDatabaseValuesAsync(ct);
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
}
