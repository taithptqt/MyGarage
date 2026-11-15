using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class AiAuthorizationService : IAiAuthorizationService
{
    private readonly IGarageContext _context;

    public AiAuthorizationService(IGarageContext context)
    {
        _context = context;
    }

    private static bool IsAdminActor(UserActorContext actor)
    {
        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        if (actor.IsAdmin || string.Equals(actor.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(actor.RoleCode, "ADMIN", StringComparison.OrdinalIgnoreCase);
    }

    public Task<bool> CanManageAiModelConfigsAsync(UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(IsAdminActor(actor));
    }

    public Task<bool> CanManageAiKnowledgeBaseAsync(UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(IsAdminActor(actor));
    }

    public Task<bool> CanAccessAiForecastsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        if (IsAdminActor(actor))
        {
            return Task.FromResult(true);
        }

        if (string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            if (!branchId.HasValue || branchId.Value <= 0)
            {
                return Task.FromResult(true);
            }

            if (!actor.BranchId.HasValue || actor.BranchId.Value <= 0)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(actor.BranchId.Value == branchId.Value);
        }

        return Task.FromResult(false);
    }

    public Task<bool> CanAccessSmartDispatchingAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        if (IsAdminActor(actor))
        {
            return Task.FromResult(true);
        }

        if (string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            if (!branchId.HasValue || branchId.Value <= 0)
            {
                return Task.FromResult(true);
            }

            if (!actor.BranchId.HasValue || actor.BranchId.Value <= 0)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(actor.BranchId.Value == branchId.Value);
        }

        return Task.FromResult(false);
    }

    public async Task<bool> CanAccessChatbotSessionAsync(long customerId, long sessionId, UserActorContext actor, CancellationToken ct = default)
    {
        if (customerId <= 0 || sessionId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        if (IsAdminActor(actor))
        {
            return true;
        }

        if (string.Equals(actor.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            if (actor.UserId.Value != customerId)
            {
                return false;
            }

            return await _context.ChatbotSessions
                .AsNoTracking()
                .AnyAsync(s => s.Id == sessionId && s.CustomerId == customerId, ct);
        }

        return false;
    }
}
