using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfMaterialRequestQueryService(IGarageContext context) : IMaterialRequestQueryService
{
    public async Task<(IReadOnlyList<MaterialRequest> Items, int Total)> GetPageAsync(
        long? scopedBranchId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.MaterialRequests
            .AsNoTracking()
            .AsQueryable();

        if (scopedBranchId.HasValue)
        {
            query = query.Where(mr =>
                (mr.ServiceOrder != null && mr.ServiceOrder.ExecuteBranchId == scopedBranchId.Value) ||
                mr.MaterialRequestDetails.Any(d => d.Warehouse.BranchId == scopedBranchId.Value));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(mr => mr.Status != null && mr.Status.ToUpper() == normalizedStatus);
        }

        var ordered = query
            .Include(mr => mr.ServiceOrder)
            .Include(mr => mr.Technician)
            .Include(mr => mr.Storekeeper)
            .Include(mr => mr.MaterialRequestDetails)
                .ThenInclude(d => d.Product)
            .OrderByDescending(mr => mr.CreatedAt ?? mr.RequestDate)
            .ThenByDescending(mr => mr.Id);

        var total = await ordered.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total
            ? []
            : await ordered.Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return (items, total);
    }

    public Task<MaterialRequest?> GetByIdDetailedAsync(long id, CancellationToken ct = default)
        => context.MaterialRequests
            .AsNoTracking()
            .Include(x => x.ServiceOrder)
            .Include(x => x.Technician)
            .Include(x => x.Storekeeper)
            .Include(x => x.MaterialRequestDetails)
                .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<MaterialRequest?> GetWithDetailsByIdAsync(long id, CancellationToken ct = default)
        => context.MaterialRequests
            .AsNoTracking()
            .Include(x => x.MaterialRequestDetails)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> ExistsAsync(long id, CancellationToken ct = default)
        => context.MaterialRequests.AsNoTracking().AnyAsync(x => x.Id == id, ct);

    public Task<long?> GetServiceOrderExecuteBranchIdAsync(long serviceOrderId, CancellationToken ct = default)
        => context.ServiceOrders
            .AsNoTracking()
            .Where(so => so.Id == serviceOrderId)
            .Select(so => (long?)so.ExecuteBranchId)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> BelongsToBranchScopeAsync(long materialRequestId, long? scopedBranchId, CancellationToken ct = default)
    {
        if (!scopedBranchId.HasValue)
        {
            return true;
        }

        return await context.MaterialRequests
            .AsNoTracking()
            .AnyAsync(mr =>
                mr.Id == materialRequestId &&
                ((mr.ServiceOrder != null && mr.ServiceOrder.ExecuteBranchId == scopedBranchId.Value) ||
                 mr.MaterialRequestDetails.Any(d => d.Warehouse.BranchId == scopedBranchId.Value) ||
                 (mr.Technician != null && mr.Technician.BranchId == scopedBranchId.Value)), ct);
    }

    public async Task<Dictionary<(long ProductId, long WarehouseId), MyGarage.Models.Inventory>> LoadInventoryLookupAsync(
        IEnumerable<MaterialRequest> requests,
        CancellationToken ct = default)
    {
        var pairs = requests
            .SelectMany(r => r.MaterialRequestDetails)
            .Select(d => new { d.ProductId, d.WarehouseId })
            .Distinct()
            .ToList();

        if (pairs.Count == 0)
        {
            return new Dictionary<(long ProductId, long WarehouseId), MyGarage.Models.Inventory>();
        }

        var productIds = pairs.Select(p => p.ProductId).Distinct().ToList();
        var warehouseIds = pairs.Select(p => p.WarehouseId).Distinct().ToList();

        var inventories = await context.Inventories
            .AsNoTracking()
            .Where(inv => productIds.Contains(inv.ProductId) && warehouseIds.Contains(inv.WarehouseId))
            .ToListAsync(ct);

        return inventories.ToDictionary(inv => (inv.ProductId, inv.WarehouseId), inv => inv);
    }
}
