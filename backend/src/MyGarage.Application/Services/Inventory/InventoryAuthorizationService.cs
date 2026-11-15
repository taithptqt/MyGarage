using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Inventory;

namespace MyGarage.Services;

public sealed class InventoryAuthorizationService : IInventoryAuthorizationService
{
    private readonly IInventoryOperationsStore _store;

    public InventoryAuthorizationService(IInventoryOperationsStore store)
    {
        _store = store;
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

        var branchId = await _store.GetWarehouseBranchIdAsync(warehouseId, cancellationToken);
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

        var (found, serviceOrderId) = await _store.GetMaterialRequestServiceOrderIdAsync(materialRequestId, cancellationToken);
        if (!found)
        {
            return false;
        }

        if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
        {
            var soBranch = await _store.GetServiceOrderExecuteBranchIdAsync(serviceOrderId.Value, cancellationToken);
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

        var soBranch = await _store.GetServiceOrderExecuteBranchIdAsync(serviceOrderId, cancellationToken);
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
