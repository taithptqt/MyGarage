using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfRetailReturnItemQueryService(IGarageContext context) : IRetailReturnItemQueryService
{
    public async Task<(IReadOnlyList<RetailReturnItem> Items, int Total)> GetPageAsync(long? branchId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.RetailReturnItems.AsNoTracking().AsQueryable();
        if (branchId.HasValue) query = query.Where(x => x.RetailReturn.RetailOrder.BranchId == branchId.Value);
        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total
            ? []
            : await query.OrderByDescending(x => x.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<RetailReturnItem?> GetByIdScopedAsync(long id, long? branchId, CancellationToken ct = default)
        => context.RetailReturnItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && (!branchId.HasValue || x.RetailReturn.RetailOrder.BranchId == branchId.Value), ct);

    public Task<RetailReturnParentInfo?> GetParentForCreateAsync(long retailReturnId, long? branchId, CancellationToken ct = default)
        => context.RetailReturns.AsNoTracking()
            .Where(x => x.Id == retailReturnId && (!branchId.HasValue || x.RetailOrder.BranchId == branchId.Value))
            .Select(x => new RetailReturnParentInfo(x.Status, x.RetailOrderId, x.RetailOrder.BranchId))
            .FirstOrDefaultAsync(ct);

    public async Task<bool> ValidateOrderItemAndWarehouseAsync(long retailOrderItemId, long retailOrderId, long warehouseId, long branchId, CancellationToken ct = default)
    {
        var hasOrderItem = await context.RetailOrderItems.AsNoTracking()
            .AnyAsync(x => x.Id == retailOrderItemId && x.RetailOrderId == retailOrderId, ct);
        if (!hasOrderItem) return false;

        return await WarehouseBelongsToBranchAsync(warehouseId, branchId, ct);
    }

    public Task<RetailReturnItem?> GetForUpdateScopedAsync(long id, long? branchId, CancellationToken ct = default)
        => context.RetailReturnItems.Include(x => x.RetailReturn).ThenInclude(x => x.RetailOrder)
            .FirstOrDefaultAsync(x => x.Id == id && (!branchId.HasValue || x.RetailReturn.RetailOrder.BranchId == branchId.Value), ct);

    public Task<bool> WarehouseBelongsToBranchAsync(long warehouseId, long branchId, CancellationToken ct = default)
        => context.Warehouses.AsNoTracking().AnyAsync(x => x.Id == warehouseId && x.BranchId == branchId, ct);

    public Task<string?> GetParentStatusScopedAsync(long id, long? branchId, CancellationToken ct = default)
        => context.RetailReturnItems.AsNoTracking()
            .Where(x => x.Id == id && (!branchId.HasValue || x.RetailReturn.RetailOrder.BranchId == branchId.Value))
            .Select(x => x.RetailReturn.Status)
            .FirstOrDefaultAsync(ct);
}
