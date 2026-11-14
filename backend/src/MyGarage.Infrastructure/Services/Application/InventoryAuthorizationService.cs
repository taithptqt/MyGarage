using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class InventoryAuthorizationService : IInventoryAuthorizationService
{
    private readonly IGarageContext _context;

    public InventoryAuthorizationService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<bool> CanAccessWarehouseAsync(long warehouseId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (warehouseId <= 0 || !actor.UserId.HasValue)
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

        var branchId = await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.Id == warehouseId)
            .Select(w => (long?)w.BranchId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!branchId.HasValue)
        {
            return false;
        }

        return !actor.BranchId.HasValue || actor.BranchId.Value == branchId.Value;
    }

    public Task<bool> CanAccessBranchInventoryAsync(long branchId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (branchId <= 0 || !actor.UserId.HasValue)
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

        return Task.FromResult(!actor.BranchId.HasValue || actor.BranchId.Value == branchId);
    }

    public async Task<bool> CanMutateInventoryAsync(long warehouseId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessWarehouseAsync(warehouseId, actor, cancellationToken))
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        return true;
    }

    public Task<bool> CanViewStockTransactionsAsync(long? branchId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (!actor.UserId.HasValue)
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

        if (branchId.HasValue && branchId.Value > 0)
        {
            if (actor.BranchId.HasValue && actor.BranchId.Value != branchId.Value)
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    public Task<bool> CanManageStockAlertsAsync(long? branchId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (!actor.UserId.HasValue)
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

        if (branchId.HasValue && branchId.Value > 0)
        {
            if (actor.BranchId.HasValue && actor.BranchId.Value != branchId.Value)
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    public async Task<bool> CanIssueMaterialRequestAsync(long materialRequestId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (materialRequestId <= 0 || !actor.UserId.HasValue)
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

        var mr = await _context.MaterialRequests
            .AsNoTracking()
            .Where(r => r.Id == materialRequestId)
            .Select(r => new { r.ServiceOrderId })
            .FirstOrDefaultAsync(cancellationToken);

        if (mr == null)
        {
            return false;
        }

        if (mr.ServiceOrderId.HasValue && mr.ServiceOrderId.Value > 0)
        {
            var soBranch = await _context.ServiceOrders
                .AsNoTracking()
                .Where(so => so.Id == mr.ServiceOrderId.Value)
                .Select(so => (long?)so.ExecuteBranchId)
                .FirstOrDefaultAsync(cancellationToken);

            if (soBranch.HasValue && actor.BranchId.HasValue && actor.BranchId.Value != soBranch.Value)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<bool> CanProcessMaterialReturnAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (serviceOrderId <= 0 || !actor.UserId.HasValue)
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

        var soBranch = await _context.ServiceOrders
            .AsNoTracking()
            .Where(so => so.Id == serviceOrderId)
            .Select(so => (long?)so.ExecuteBranchId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!soBranch.HasValue)
        {
            return false;
        }

        if (actor.BranchId.HasValue && actor.BranchId.Value != soBranch.Value)
        {
            return false;
        }

        return true;
    }
}
