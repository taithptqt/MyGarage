using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfPurchaseOrderItemQueryService : IPurchaseOrderItemQueryService
{
    private readonly IGarageContext _context;

    public EfPurchaseOrderItemQueryService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PurchaseOrderItem>> GetAllByBranchAsync(long? branchId, CancellationToken ct = default)
    {
        var query = _context.PurchaseOrderItems.AsNoTracking();
        if (branchId.HasValue)
        {
            query = query.Where(item => item.PurchaseOrder.BranchId == branchId.Value);
        }

        return await query.ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<PurchaseOrderItem> Items, int Total)> GetPageByBranchAsync(
        long? branchId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.PurchaseOrderItems.AsNoTracking();
        if (branchId.HasValue)
            query = query.Where(item => item.PurchaseOrder.BranchId == branchId.Value);

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= total) return (Array.Empty<PurchaseOrderItem>(), total);
        var items = await query.OrderBy(item => item.Id)
            .Skip((int)offset).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}
