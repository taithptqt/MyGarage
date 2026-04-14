using System;
using System.Security.Claims;
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
        private readonly IGarageContext _context;

        public ChatHub(IGarageContext context)
        {
            _context = context;
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
            if (string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                return await _context.Conversations.AsNoTracking().AnyAsync(c => c.Id == conversationId && c.CustomerId == userId);
            }

            if (!string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                return false;
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
            if (!string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase))
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
    }
}
