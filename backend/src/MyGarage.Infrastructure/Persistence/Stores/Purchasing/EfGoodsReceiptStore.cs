using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Purchasing;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Purchasing;

public sealed class EfGoodsReceiptStore : IGoodsReceiptStore
{
    private readonly IGarageContext _context;

    public EfGoodsReceiptStore(IGarageContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<GoodsReceiptNote> Items, int TotalCount)> GetGoodsReceiptsPageAsync(
        long? branchId,
        long? purchaseOrderId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.GoodsReceiptNotes
            .AsNoTracking()
            .Include(g => g.PurchaseOrder)
            .Include(g => g.Warehouse)
            .Include(g => g.ReceivedByStaff)
            .Include(g => g.Items)
                .ThenInclude(i => i.Product)
            .AsQueryable();

        if (branchId.HasValue)
        {
            query = query.Where(g =>
                (g.Warehouse != null && g.Warehouse.BranchId == branchId.Value) ||
                (g.PurchaseOrder != null && g.PurchaseOrder.BranchId == branchId.Value));
        }

        if (purchaseOrderId.HasValue && purchaseOrderId.Value > 0)
        {
            query = query.Where(g => g.PurchaseOrderId == purchaseOrderId.Value);
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= total)
        {
            return (System.Array.Empty<GoodsReceiptNote>(), total);
        }

        var rows = await query
            .OrderByDescending(g => g.ReceivedAt)
            .ThenByDescending(g => g.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(ct);

        return (rows, total);
    }

    public async Task<GoodsReceiptNote?> GetGoodsReceiptByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.GoodsReceiptNotes
            .AsNoTracking()
            .Include(g => g.PurchaseOrder)
            .Include(g => g.Warehouse)
            .Include(g => g.ReceivedByStaff)
            .Include(g => g.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task<bool> PurchaseOrderExistsAsync(long purchaseOrderId, CancellationToken ct = default)
    {
        return await _context.PurchaseOrders
            .AsNoTracking()
            .AnyAsync(po => po.Id == purchaseOrderId, ct);
    }
}
