using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class PurchaseOrderAuthorizationService : IPurchaseOrderAuthorizationService
{
    private static readonly string[] ManagerKeywords =
    {
        "ADMIN", "MANAGER", "SERVICE_MANAGER", "OWNER", "DIRECTOR", "QUAN_DOC"
    };

    private static readonly string[] WarehouseKeywords =
    {
        "ADMIN", "MANAGER", "WAREHOUSE", "STOREKEEPER", "STORE_KEEPER", "INVENTORY", "THU_KHO"
    };

    private readonly IGarageContext _context;

    public PurchaseOrderAuthorizationService(IGarageContext context)
    {
        _context = context;
    }

    private static bool IsStaff(UserActorContext actor)
    {
        if (!actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;
        return string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasKeyword(UserActorContext actor, string[] keywords)
    {
        if (actor.IsAdmin) return true;
        var role = (actor.Role ?? string.Empty).ToUpperInvariant();
        var code = (actor.RoleCode ?? string.Empty).ToUpperInvariant();
        return keywords.Any(k => role.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                 code.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    public Task<bool> CanViewPurchaseOrdersAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!IsStaff(actor)) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (!actor.BranchId.HasValue || actor.BranchId.Value <= 0 ||
            (branchId.HasValue && branchId.Value != actor.BranchId.Value))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public async Task<bool> CanViewPurchaseOrderAsync(long purchaseOrderId, UserActorContext actor, CancellationToken ct = default)
    {
        if (purchaseOrderId <= 0 || !IsStaff(actor)) return false;
        if (actor.IsAdmin) return true;

        var poBranchId = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Id == purchaseOrderId)
            .Select(po => (long?)po.BranchId)
            .FirstOrDefaultAsync(ct);

        if (!poBranchId.HasValue) return false;
        return actor.BranchId.HasValue && actor.BranchId.Value > 0 && actor.BranchId.Value == poBranchId.Value;
    }

    public Task<bool> CanCreatePurchaseOrderAsync(long branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (branchId <= 0 || !IsStaff(actor)) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        return Task.FromResult(actor.BranchId.HasValue && actor.BranchId.Value > 0 && actor.BranchId.Value == branchId);
    }

    public async Task<bool> CanUpdatePurchaseOrderAsync(long purchaseOrderId, UserActorContext actor, CancellationToken ct = default)
    {
        if (purchaseOrderId <= 0 || !IsStaff(actor)) return false;
        if (actor.IsAdmin) return true;

        var poBranchId = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Id == purchaseOrderId)
            .Select(po => (long?)po.BranchId)
            .FirstOrDefaultAsync(ct);

        if (!poBranchId.HasValue) return false;
        return actor.BranchId.HasValue && actor.BranchId.Value > 0 && actor.BranchId.Value == poBranchId.Value;
    }

    public async Task<bool> CanApprovePurchaseOrderAsync(long purchaseOrderId, UserActorContext actor, CancellationToken ct = default)
    {
        if (purchaseOrderId <= 0 || !IsStaff(actor)) return false;
        if (!HasKeyword(actor, ManagerKeywords)) return false;
        if (actor.IsAdmin) return true;

        var poBranchId = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Id == purchaseOrderId)
            .Select(po => (long?)po.BranchId)
            .FirstOrDefaultAsync(ct);

        if (!poBranchId.HasValue) return false;
        return actor.BranchId.HasValue && actor.BranchId.Value > 0 && actor.BranchId.Value == poBranchId.Value;
    }

    public async Task<bool> CanReceiveGoodsAsync(long purchaseOrderId, UserActorContext actor, CancellationToken ct = default)
    {
        if (purchaseOrderId <= 0 || !IsStaff(actor)) return false;
        if (!HasKeyword(actor, WarehouseKeywords)) return false;
        if (actor.IsAdmin) return true;

        var poBranchId = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Id == purchaseOrderId)
            .Select(po => (long?)po.BranchId)
            .FirstOrDefaultAsync(ct);

        if (!poBranchId.HasValue) return false;
        return actor.BranchId.HasValue && actor.BranchId.Value > 0 && actor.BranchId.Value == poBranchId.Value;
    }

    public async Task<bool> CanCancelOrDeletePurchaseOrderAsync(long purchaseOrderId, UserActorContext actor, CancellationToken ct = default)
    {
        if (purchaseOrderId <= 0 || !IsStaff(actor)) return false;
        if (!HasKeyword(actor, ManagerKeywords)) return false;
        if (actor.IsAdmin) return true;

        var poBranchId = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Id == purchaseOrderId)
            .Select(po => (long?)po.BranchId)
            .FirstOrDefaultAsync(ct);

        if (!poBranchId.HasValue) return false;
        return actor.BranchId.HasValue && actor.BranchId.Value > 0 && actor.BranchId.Value == poBranchId.Value;
    }

    public Task<bool> CanViewGoodsReceiptsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!IsStaff(actor)) return Task.FromResult(false);
        if (actor.IsAdmin) return Task.FromResult(true);

        if (!actor.BranchId.HasValue || actor.BranchId.Value <= 0 ||
            (branchId.HasValue && branchId.Value != actor.BranchId.Value))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public async Task<bool> CanViewGoodsReceiptAsync(long goodsReceiptId, UserActorContext actor, CancellationToken ct = default)
    {
        if (goodsReceiptId <= 0 || !IsStaff(actor)) return false;
        if (actor.IsAdmin) return true;

        var grn = await _context.GoodsReceiptNotes
            .AsNoTracking()
            .Where(g => g.Id == goodsReceiptId)
            .Select(g => new
            {
                WarehouseBranchId = (long?)g.Warehouse.BranchId,
                PoBranchId = (long?)g.PurchaseOrder.BranchId
            })
            .FirstOrDefaultAsync(ct);

        if (grn == null) return false;
        var branchId = grn.WarehouseBranchId ?? grn.PoBranchId;
        if (!branchId.HasValue) return false;

        return actor.BranchId.HasValue && actor.BranchId.Value > 0 && actor.BranchId.Value == branchId.Value;
    }
}
