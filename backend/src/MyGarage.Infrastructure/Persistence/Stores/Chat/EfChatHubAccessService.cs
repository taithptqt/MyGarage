using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Services.Chat;
using MyGarage.Data;

namespace MyGarage.Infrastructure.Persistence.Stores.Chat;

public sealed class EfChatHubAccessService : IChatHubAccessService
{
    private readonly GarageContext _context;

    public EfChatHubAccessService(GarageContext context)
    {
        _context = context;
    }

    public async Task<(bool Found, bool Allowed)> CanAccessConversationAsync(
        long userId,
        string role,
        string roleCode,
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _context.Conversations
            .AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new { c.Id, c.CustomerId, c.BranchId })
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            return (false, false);
        }

        if (IsCustomerRole(role, roleCode))
        {
            return (true, conversation.CustomerId == userId);
        }

        var staff = await _context.Staffs
            .AsNoTracking()
            .Include(s => s.Role)
            .FirstOrDefaultAsync(s => s.Id == userId && s.IsDeleted != true, cancellationToken);

        if (staff?.BranchId is null)
        {
            return (true, false);
        }

        if (!IsReceptionistRole(roleCode, staff.Role?.RoleCode, staff.Role?.RoleName))
        {
            return (true, false);
        }

        return (true, conversation.BranchId.HasValue && staff.BranchId.Value == conversation.BranchId.Value);
    }

    public async Task<bool> CanAccessBranchInboxAsync(
        long userId,
        string role,
        string roleCode,
        long branchId,
        CancellationToken cancellationToken = default)
    {
        if (IsCustomerRole(role, roleCode))
        {
            return false;
        }

        var staff = await _context.Staffs
            .AsNoTracking()
            .Include(s => s.Role)
            .FirstOrDefaultAsync(s => s.Id == userId && s.IsDeleted != true, cancellationToken);

        if (staff is null || staff.BranchId != branchId)
        {
            return false;
        }

        return IsReceptionistRole(roleCode, staff.Role?.RoleCode, staff.Role?.RoleName);
    }

    public async Task<bool> CanAccessWorkshopBranchAsync(
        long userId,
        string role,
        string roleCode,
        long branchId,
        CancellationToken cancellationToken = default)
    {
        if (IsCustomerRole(role, roleCode))
        {
            return false;
        }

        var staff = await _context.Staffs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == userId && s.IsDeleted != true, cancellationToken);

        return staff is not null && staff.BranchId == branchId;
    }

    public async Task<IReadOnlyList<ChatConversationBranchRef>> GetCustomerConversationRefsAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => new ChatConversationBranchRef(c.Id, c.BranchId))
            .ToListAsync(cancellationToken);
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

    private static bool IsCustomerRole(params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalized = value.Trim().ToUpperInvariant();
            var compact = normalized.Replace(" ", string.Empty);
            if (normalized == "CUSTOMER" || compact == "KHACHHANG")
            {
                return true;
            }
        }

        return false;
    }
}
