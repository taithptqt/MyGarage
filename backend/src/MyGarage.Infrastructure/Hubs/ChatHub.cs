using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using MyGarage.Interfaces;

namespace MyGarage.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, byte> ActiveConnectionRegistry = new();
        private static readonly ConcurrentDictionary<long, int> ActiveCustomerConnections = new();
        private readonly IGarageContext _context;

        public ChatHub(IGarageContext context)
        {
            _context = context;
        }

        public static bool IsCustomerOnline(long customerId)
        {
            return customerId > 0
                && ActiveCustomerConnections.TryGetValue(customerId, out var count)
                && count > 0;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = ResolveCurrentUserId();
            var role = ResolveCurrentRole();
            var roleCode = ResolveCurrentRoleCode();

            if (userId.HasValue)
            {
                var key = BuildConnectionKey(Context.ConnectionId, role, roleCode, userId.Value);
                ActiveConnectionRegistry[key] = 0;
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId.Value}");

                if (IsCustomerRole(role, roleCode))
                {
                    ActiveCustomerConnections.AddOrUpdate(userId.Value, 1, (_, current) => current + 1);
                    await NotifyCustomerPresenceChangedAsync(userId.Value);
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = ResolveCurrentUserId();
            var role = ResolveCurrentRole();
            var roleCode = ResolveCurrentRoleCode();

            if (userId.HasValue)
            {
                var key = BuildConnectionKey(Context.ConnectionId, role, roleCode, userId.Value);
                ActiveConnectionRegistry.TryRemove(key, out _);

                if (IsCustomerRole(role, roleCode))
                {
                    ActiveCustomerConnections.AddOrUpdate(
                        userId.Value,
                        0,
                        (_, current) => current > 0 ? current - 1 : 0);

                    if (ActiveCustomerConnections.TryGetValue(userId.Value, out var remaining) && remaining <= 0)
                    {
                        ActiveCustomerConnections.TryRemove(userId.Value, out _);
                    }

                    await NotifyCustomerPresenceChangedAsync(userId.Value);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinConversationGroup(long conversationId)
        {
            var userId = ResolveCurrentUserId();
            if (!userId.HasValue)
            {
                throw new HubException("Unauthorized.");
            }

            var role = ResolveCurrentRole();
            var roleCode = ResolveCurrentRoleCode();
            var conversation = await _context.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == conversationId);
            if (conversation is null)
            {
                throw new HubException("Conversation not found.");
            }

            if (!await CanAccessConversationAsync(userId.Value, role, roleCode, conversationId, conversation.BranchId))
            {
                throw new HubException("Forbidden.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{conversationId}");
        }

        public Task LeaveConversationGroup(long conversationId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Conversation_{conversationId}");
        }

        public async Task JoinBranchInboxGroup(long branchId)
        {
            var userId = ResolveCurrentUserId();
            if (!userId.HasValue)
            {
                throw new HubException("Unauthorized.");
            }

            var role = ResolveCurrentRole();
            var roleCode = ResolveCurrentRoleCode();
            if (!await CanAccessBranchInboxAsync(userId.Value, role, roleCode, branchId))
            {
                throw new HubException("Forbidden.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"BranchInbox_{branchId}");
        }

        public async Task JoinWorkshopBranchGroup(long branchId)
        {
            var userId = ResolveCurrentUserId();
            if (!userId.HasValue)
            {
                throw new HubException("Unauthorized.");
            }

            var role = ResolveCurrentRole();
            var roleCode = ResolveCurrentRoleCode();
            if (IsCustomerRole(role, roleCode))
            {
                throw new HubException("Forbidden.");
            }

            var staff = await _context.Staffs
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == userId.Value && s.IsDeleted != true);
            if (staff is null || staff.BranchId != branchId)
            {
                throw new HubException("Forbidden.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"WorkshopBranch_{branchId}");
        }

        public async Task JoinUserGroup(long userId)
        {
            var currentUserId = ResolveCurrentUserId();
            if (!currentUserId.HasValue || currentUserId.Value != userId)
            {
                throw new HubException("Forbidden.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        private long? ResolveCurrentUserId()
        {
            var raw = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(raw, out var id) ? id : null;
        }

        private string ResolveCurrentRole()
        {
            return Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        private string ResolveCurrentRoleCode()
        {
            return Context.User?.FindFirst("role_code")?.Value ?? Context.User?.FindFirst("role_name")?.Value ?? string.Empty;
        }

        private async Task<bool> CanAccessConversationAsync(long userId, string role, string roleCode, long conversationId, long? conversationBranchId)
        {
            if (IsCustomerRole(role, roleCode))
            {
                return await _context.Conversations.AsNoTracking().AnyAsync(c => c.Id == conversationId && c.CustomerId == userId);
            }

            var staff = await _context.Staffs
                .AsNoTracking()
                .Include(s => s.Role)
                .FirstOrDefaultAsync(s => s.Id == userId && s.IsDeleted != true);

            if (staff?.BranchId is null)
            {
                return false;
            }

            if (!IsReceptionistRole(roleCode, staff.Role?.RoleCode, staff.Role?.RoleName))
            {
                return false;
            }

            return conversationBranchId.HasValue && staff.BranchId.Value == conversationBranchId.Value;
        }

        private async Task<bool> CanAccessBranchInboxAsync(long userId, string role, string roleCode, long branchId)
        {
            if (IsCustomerRole(role, roleCode))
            {
                return false;
            }

            var staff = await _context.Staffs
                .AsNoTracking()
                .Include(s => s.Role)
                .FirstOrDefaultAsync(s => s.Id == userId && s.IsDeleted != true);

            if (staff is null || staff.BranchId != branchId)
            {
                return false;
            }

            return IsReceptionistRole(roleCode, staff.Role?.RoleCode, staff.Role?.RoleName);
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

        private static string BuildConnectionKey(string connectionId, string role, string roleCode, long userId)
        {
            var normalizedRole = string.IsNullOrWhiteSpace(role) ? "-" : role.Trim().ToUpperInvariant();
            var normalizedRoleCode = string.IsNullOrWhiteSpace(roleCode) ? "-" : roleCode.Trim().ToUpperInvariant();
            return $"{connectionId}:{normalizedRole}:{normalizedRoleCode}:{userId}";
        }

        private async Task NotifyCustomerPresenceChangedAsync(long customerId)
        {
            var refs = await _context.Conversations
                .AsNoTracking()
                .Where(c => c.CustomerId == customerId)
                .Select(c => new { c.Id, c.BranchId })
                .ToListAsync();

            foreach (var item in refs)
            {
                await Clients.Group($"Conversation_{item.Id}")
                    .SendAsync("ConversationUpdated", new { conversationId = item.Id });

                if (item.BranchId.HasValue)
                {
                    await Clients.Group($"BranchInbox_{item.BranchId.Value}")
                        .SendAsync("ConversationUpdated", new { conversationId = item.Id });
                }
            }
        }
    }
}
