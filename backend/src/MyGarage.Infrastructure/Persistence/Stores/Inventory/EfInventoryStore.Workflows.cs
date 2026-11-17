using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Inventory;

public sealed partial class EfInventoryStore
{
    public async Task<long?> GetWarehouseBranchIdAsync(long warehouseId, CancellationToken ct = default)
    {
        return await _db.Warehouses
            .AsNoTracking()
            .Where(w => w.Id == warehouseId)
            .Select(w => (long?)w.BranchId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(bool Found, long? ServiceOrderId)> GetMaterialRequestServiceOrderIdAsync(long materialRequestId, CancellationToken ct = default)
    {
        var mr = await _db.MaterialRequests
            .AsNoTracking()
            .Where(r => r.Id == materialRequestId)
            .Select(r => new { r.ServiceOrderId })
            .FirstOrDefaultAsync(ct);

        return mr == null ? (false, null) : (true, mr.ServiceOrderId);
    }

    public async Task<long?> GetServiceOrderExecuteBranchIdAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return await _db.ServiceOrders
            .AsNoTracking()
            .Where(so => so.Id == serviceOrderId)
            .Select(so => (long?)so.ExecuteBranchId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<GoodsReceiptNote>> GetGoodsReceiptNotesForSnapshotAsync(
        DateTime start,
        DateTime end,
        long? branchId,
        CancellationToken ct = default)
    {
        var query = _db.GoodsReceiptNotes
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Where(x => x.ReceivedAt >= start && x.ReceivedAt < end);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(x => x.Warehouse.BranchId == branchId.Value);
        }

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<GoodsReceiptNoteItem>> GetGoodsReceiptNoteItemsAsync(
        IReadOnlyCollection<long> grnIds,
        CancellationToken ct = default)
    {
        if (grnIds.Count == 0)
        {
            return Array.Empty<GoodsReceiptNoteItem>();
        }

        return await _db.GoodsReceiptNoteItems
            .AsNoTracking()
            .Where(x => grnIds.Contains(x.GoodsReceiptNoteId))
            .ToListAsync(ct);
    }

    public Task<GrnDailySnapshot?> GetGrnDailySnapshotAsync(long branchId, DateOnly snapshotDate, CancellationToken ct = default)
    {
        return _db.GrnDailySnapshots
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.SnapshotDate == snapshotDate, ct);
    }

    public void AddGrnDailySnapshot(GrnDailySnapshot snapshot)
    {
        _db.GrnDailySnapshots.Add(snapshot);
    }

    public void RemoveGrnDailySnapshot(GrnDailySnapshot snapshot)
    {
        _db.GrnDailySnapshots.Remove(snapshot);
    }

    public async Task<IReadOnlyList<GrnDailySnapshot>> GetGrnDailySnapshotsRangeAsync(
        DateOnly fromDate,
        DateOnly toDate,
        long? branchId,
        CancellationToken ct = default)
    {
        var query = _db.GrnDailySnapshots
            .AsNoTracking()
            .Where(x => x.SnapshotDate >= fromDate && x.SnapshotDate <= toDate);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        return await query
            .OrderBy(x => x.SnapshotDate)
            .ThenBy(x => x.BranchId)
            .ToListAsync(ct);
    }

    public Task<Quotation?> GetQuotationWithDetailsAndServiceOrderAsync(long quotationId, CancellationToken ct = default)
    {
        return _db.Quotations
            .Include(q => q.QuotationDetails)
            .Include(q => q.ServiceOrder)
            .FirstOrDefaultAsync(q => q.Id == quotationId, ct);
    }

    public Task<MaterialRequest?> GetMaterialRequestByIdAsync(long requestId, CancellationToken ct = default)
    {
        var requests = _db.Database.IsSqlServer() && _db.Database.CurrentTransaction != null
            ? _db.MaterialRequests.FromSqlInterpolated($"SELECT * FROM dbo.MaterialRequests WITH (UPDLOCK, ROWLOCK) WHERE Id = {requestId}")
            : _db.MaterialRequests.AsQueryable();
        return requests
            .FirstOrDefaultAsync(m => m.Id == requestId, ct);
    }

    public async Task<IReadOnlyList<ServiceOrderDetail>> GetQuotationOrderDetailsAsync(long quotationId, CancellationToken ct = default)
    {
        return await _db.ServiceOrderDetails
            .Where(d => d.SourceQuotationId == quotationId && d.QuotationDetailId != null)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MaterialRequestDetail>> GetRequestsForOrderDetailsAsync(IReadOnlyCollection<long> orderDetailIds, CancellationToken ct = default)
    {
        return await _db.MaterialRequestDetails.Include(d => d.Request)
            .Where(d => d.ServiceOrderDetailId.HasValue && orderDetailIds.Contains(d.ServiceOrderDetailId.Value))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MaterialRequestDetail>> GetUnlinkedRequestsForOrderAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return await _db.MaterialRequestDetails.Include(d => d.Request)
            .Where(d => d.ServiceOrderDetailId == null && d.Request.ServiceOrderId == serviceOrderId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MaterialRequestDetail>> GetMaterialRequestDetailsAsync(long requestId, CancellationToken ct = default)
    {
        return await _db.MaterialRequestDetails
            .Where(d => d.RequestId == requestId)
            .ToListAsync(ct);
    }

    public Task<MyGarage.Models.Inventory?> GetTrackedInventoryAsync(long productId, long warehouseId, CancellationToken ct = default)
    {
        var inventories = _db.Database.IsSqlServer() && _db.Database.CurrentTransaction != null
            ? _db.Inventories.FromSqlInterpolated($"SELECT * FROM dbo.Inventories WITH (UPDLOCK, ROWLOCK) WHERE ProductId = {productId} AND WarehouseId = {warehouseId}")
            : _db.Inventories.AsQueryable();
        return inventories
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId, ct);
    }
}
