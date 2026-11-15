using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Inventory;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Inventory;

public sealed partial class EfInventoryStore
{
    public async Task<IReadOnlyList<MyGarage.Models.Inventory>> GetLowStockInventoriesForAlertsAsync(int take, CancellationToken ct = default)
    {
        return await _db.Inventories
            .AsNoTracking()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
                .ThenInclude(w => w.Branch)
            .Where(i => i.MinStockLevel.HasValue && i.Quantity <= i.MinStockLevel.Value)
            .OrderBy(i => i.Quantity)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StockTransaction>> GetStockTransactionsWithoutSourceAsync(int take, CancellationToken ct = default)
    {
        return await _db.StockTransactions
            .AsNoTracking()
            .Where(t =>
                !t.RetailOrderId.HasValue &&
                !t.RetailReturnId.HasValue &&
                !t.MaterialRequestId.HasValue &&
                !t.MaterialReturnId.HasValue &&
                !t.PurchaseOrderId.HasValue)
            .OrderByDescending(t => t.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApprovalRequest>> GetApprovalRequestsAsync(string? normalizedStatus, CancellationToken ct = default)
    {
        var query = _db.ApprovalRequests
            .AsNoTracking()
            .Include(a => a.RequestedByNavigation)
            .Include(a => a.Approver)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(a => a.Status != null && a.Status.ToUpper() == normalizedStatus);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<ApprovalRequest?> GetApprovalRequestByIdAsync(long id, CancellationToken ct = default)
    {
        return _db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<IReadOnlyList<InventoryAudit>> GetInventoryAuditsAsync(string? normalizedStatus, CancellationToken ct = default)
    {
        var query = _db.InventoryAudits
            .AsNoTracking()
            .Include(a => a.Warehouse)
                .ThenInclude(w => w.Branch)
            .Include(a => a.InventoryAuditDetails)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(a => a.Status.ToUpper() == normalizedStatus);
        }

        return await query
            .OrderByDescending(a => a.AuditDate)
            .ToListAsync(ct);
    }

    public Task<InventoryAudit?> GetInventoryAuditByIdAsync(long id, CancellationToken ct = default)
    {
        return _db.InventoryAudits.FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<IReadOnlyList<BranchSummaryRowData>> GetActiveBranchesForSummaryAsync(CancellationToken ct = default)
    {
        return await _db.Branches
            .AsNoTracking()
            .Where(b => b.IsDeleted != true)
            .OrderBy(b => b.Name)
            .Select(g => new BranchSummaryRowData(g.Id, g.Name, g.Status))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<long, int>> GetActiveWarehouseCountsByBranchAsync(CancellationToken ct = default)
    {
        return await _db.Warehouses
            .AsNoTracking()
            .Where(w => w.IsDeleted != true)
            .GroupBy(w => w.BranchId)
            .Select(g => new { branchId = g.Key, warehouseCount = g.Count() })
            .ToDictionaryAsync(x => x.branchId, x => x.warehouseCount, ct);
    }

    public async Task<IReadOnlyDictionary<long, BranchInventoryAggregateData>> GetBranchInventoryAggregatesAsync(CancellationToken ct = default)
    {
        return await _db.Inventories
            .AsNoTracking()
            .Include(i => i.Warehouse)
            .GroupBy(i => i.Warehouse.BranchId)
            .Select(g => new BranchInventoryAggregateData(
                g.Key,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.ReservedQuantity ?? 0),
                g.Sum(x => x.TotalInventoryValue ?? (x.Quantity * (x.CurrentMacPrice ?? 0))),
                g.Count(x => x.MinStockLevel.HasValue && x.Quantity <= x.MinStockLevel.Value),
                g.Count(x => x.Quantity < 0)))
            .ToDictionaryAsync(x => x.BranchId, x => x, ct);
    }

    public async Task<IReadOnlyList<MyGarage.Models.Inventory>> GetInventoryRowsForReportAsync(CancellationToken ct = default)
    {
        return await _db.Inventories
            .AsNoTracking()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
                .ThenInclude(w => w.Branch)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StockTransaction>> GetTransactionsForReportAsync(CancellationToken ct = default)
    {
        return await _db.StockTransactions
            .AsNoTracking()
            .Include(t => t.Product)
            .ToListAsync(ct);
    }

    public Task<int> CountPendingApprovalsAsync(string status, CancellationToken ct = default)
    {
        return _db.ApprovalRequests
            .AsNoTracking()
            .CountAsync(a => a.Status != null && a.Status.ToUpper() == status, ct);
    }

    public Task<int> CountPendingAuditsAsync(string status1, string status2, CancellationToken ct = default)
    {
        return _db.InventoryAudits
            .AsNoTracking()
            .CountAsync(a => a.Status != null && (a.Status.ToUpper() == status1 || a.Status.ToUpper() == status2), ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetInventoryAuditLogsAsync(IReadOnlyCollection<string> tableNames, int take, CancellationToken ct = default)
    {
        return await _db.AuditLogs
            .AsNoTracking()
            .Where(l => tableNames.Contains(l.TableName))
            .OrderByDescending(l => l.Timestamp)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StockAlert>> GetAdminStockAlertsAsync(
        bool? resolved,
        int take,
        DateTime defaultCreatedAt,
        CancellationToken ct = default)
    {
        var query = _db.StockAlerts
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
                .ThenInclude(x => x.Branch)
            .AsQueryable();

        if (resolved.HasValue)
        {
            query = query.Where(x => x.IsResolved == resolved.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt ?? defaultCreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MyGarage.Models.Inventory>> GetActiveInventoriesForAdminAlertsAsync(CancellationToken ct = default)
    {
        return await _db.Inventories
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .Where(x => x.Warehouse.IsDeleted != true && x.Warehouse.IsActive != false)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StockAlert>> GetUnresolvedStockAlertsTrackedAsync(CancellationToken ct = default)
    {
        return await _db.StockAlerts
            .Where(x => x.IsResolved == false)
            .ToListAsync(ct);
    }

    public void AddStockAlert(StockAlert alert)
    {
        _db.StockAlerts.Add(alert);
    }

    public Task<StockAlert?> GetStockAlertByIdAsync(long alertId, CancellationToken ct = default)
    {
        return _db.StockAlerts.FirstOrDefaultAsync(x => x.Id == alertId, ct);
    }

    public async Task<(IReadOnlyList<WarehouseStructureRowData> Warehouses, int Total)> GetWarehouseStructurePageAsync(
        int? page,
        int? pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Warehouses
            .AsNoTracking()
            .Include(w => w.Branch)
            .Where(w => w.IsDeleted != true)
            .OrderBy(w => w.Branch.Name)
            .ThenBy(w => w.Name)
            .Select(w => new WarehouseStructureRowData(
                w.Id,
                w.Code,
                w.Name,
                w.Type,
                w.BranchId,
                w.Branch.Name,
                w.IsActive,
                w.ManagerId));

        var hasPaging = page.HasValue || pageSize.HasValue;
        if (!hasPaging)
        {
            var all = await query.ToListAsync(ct);
            return (all, all.Count);
        }

        var normalizedPage = page.GetValueOrDefault(1);
        if (normalizedPage < 1) normalizedPage = 1;

        var normalizedPageSize = pageSize.GetValueOrDefault(20);
        if (normalizedPageSize < 1) normalizedPageSize = 1;
        if (normalizedPageSize > 200) normalizedPageSize = 200;

        var total = await query.CountAsync(ct);
        var warehouses = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(ct);

        return (warehouses, total);
    }

    public async Task<IReadOnlyList<WarehouseBinCountData>> GetWarehouseBinCountsAsync(CancellationToken ct = default)
    {
        return await _db.Inventories
            .AsNoTracking()
            .Where(i => i.LocationBin != null && i.LocationBin != "")
            .GroupBy(i => new { i.WarehouseId, i.LocationBin })
            .Select(g => new WarehouseBinCountData(
                g.Key.WarehouseId,
                g.Key.LocationBin!,
                g.Select(x => x.ProductId).Distinct().Count()))
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<AdminStockTransactionRowData> Items, int Total)> GetAdminTransactionsPageAsync(
        long? branchId,
        long? warehouseId,
        long? productId,
        string? type,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default)
    {
        var query = _db.StockTransactions
            .AsNoTracking()
            .Include(t => t.Product)
            .Include(t => t.CreatedByNavigation)
            .Join(_db.Warehouses.AsNoTracking(), t => t.WarehouseId, w => w.Id, (t, w) => new { t, w })
            .Join(
                _db.Branches.AsNoTracking(),
                tw => tw.w.BranchId,
                b => b.Id,
                (tw, b) => new { Transaction = tw.t, Warehouse = tw.w, Branch = b })
            .AsQueryable();

        if (branchId.HasValue) query = query.Where(x => x.Branch.Id == branchId.Value);
        if (warehouseId.HasValue) query = query.Where(x => x.Warehouse.Id == warehouseId.Value);
        if (productId.HasValue) query = query.Where(x => x.Transaction.ProductId == productId.Value);
        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalized = type.Trim().ToUpperInvariant();
            query = query.Where(x => x.Transaction.Type != null && x.Transaction.Type.ToUpper().Contains(normalized));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x =>
                x.Transaction.Product.Sku.Contains(keyword) ||
                x.Transaction.Product.Name.Contains(keyword) ||
                (x.Transaction.Note != null && x.Transaction.Note.Contains(keyword)) ||
                (x.Transaction.CreatedByNavigation != null && x.Transaction.CreatedByNavigation.FullName.Contains(keyword)));
        }
        if (from.HasValue) query = query.Where(x => x.Transaction.CreatedAt.HasValue && x.Transaction.CreatedAt.Value >= from.Value);
        if (to.HasValue) query = query.Where(x => x.Transaction.CreatedAt.HasValue && x.Transaction.CreatedAt.Value <= to.Value);

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= total)
        {
            return (Array.Empty<AdminStockTransactionRowData>(), total);
        }

        var rows = await query
            .OrderByDescending(x => x.Transaction.CreatedAt)
            .ThenByDescending(x => x.Transaction.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .Select(x => new AdminStockTransactionRowData(x.Transaction, x.Warehouse, x.Branch))
            .ToListAsync(ct);

        return (rows, total);
    }
}
