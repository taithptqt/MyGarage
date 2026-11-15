using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class BayAuthorizationService : IBayAuthorizationService
{
    private readonly IGarageContext _context;

    public BayAuthorizationService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<bool> CanAccessBayAsync(long bayId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (bayId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var branchId = await _context.Bays
            .AsNoTracking()
            .Where(b => b.Id == bayId)
            .Select(b => (long?)b.BranchId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!branchId.HasValue)
        {
            return false;
        }

        return actor.BranchId.HasValue && actor.BranchId.Value > 0 && actor.BranchId.Value == branchId.Value;
    }

    public Task<bool> CanAccessBranchBaysAsync(long branchId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (branchId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        if (actor.IsAdmin)
        {
            return Task.FromResult(true);
        }

        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(actor.BranchId.HasValue && actor.BranchId.Value > 0 && actor.BranchId.Value == branchId);
    }

    public async Task<bool> CanAllocateBayAsync(long bayId, long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessBayAsync(bayId, actor, cancellationToken))
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        if (serviceOrderId > 0)
        {
            var orderBranchId = await _context.ServiceOrders
                .AsNoTracking()
                .Where(so => so.Id == serviceOrderId)
                .Select(so => (long?)so.ExecuteBranchId)
                .FirstOrDefaultAsync(cancellationToken);

            if (orderBranchId.HasValue && (!actor.BranchId.HasValue || actor.BranchId.Value != orderBranchId.Value))
            {
                return false;
            }
        }

        return true;
    }

    public Task<bool> CanReleaseBayAsync(long bayId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        return CanAccessBayAsync(bayId, actor, cancellationToken);
    }

    public async Task<bool> CanTransferBayAsync(long fromBayId, long toBayId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessBayAsync(fromBayId, actor, cancellationToken))
        {
            return false;
        }

        if (!await CanAccessBayAsync(toBayId, actor, cancellationToken))
        {
            return false;
        }

        return true;
    }

    public Task<bool> CanAdministerBaysAsync(long branchId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        return CanAccessBranchBaysAsync(branchId, actor, cancellationToken);
    }
}
