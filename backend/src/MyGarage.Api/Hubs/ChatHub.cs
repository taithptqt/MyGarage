using System;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MyGarage.Application.Interfaces.Services.Chat;

namespace MyGarage.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, byte> ActiveConnectionRegistry = new();
        private static readonly ConcurrentDictionary<long, int> ActiveCustomerConnections = new();
        private readonly IChatHubAccessService _chatHubAccessService;

        public ChatHub(IChatHubAccessService chatHubAccessService)
        {
            _chatHubAccessService = chatHubAccessService;
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
            var (found, allowed) = await _chatHubAccessService.CanAccessConversationAsync(
                userId.Value,
                role,
                roleCode,
                conversationId,
                Context.ConnectionAborted);

            if (!found)
            {
                throw new HubException("Conversation not found.");
            }

            if (!allowed)
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
            if (!await _chatHubAccessService.CanAccessBranchInboxAsync(
                userId.Value,
                role,
                roleCode,
                branchId,
                Context.ConnectionAborted))
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
            if (!await _chatHubAccessService.CanAccessWorkshopBranchAsync(
                userId.Value,
                role,
                roleCode,
                branchId,
                Context.ConnectionAborted))
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
            var refs = await _chatHubAccessService.GetCustomerConversationRefsAsync(customerId, Context.ConnectionAborted);

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
