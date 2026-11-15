using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Purchasing;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Purchasing;

public sealed class EfPurchaseOrderStore : IPurchaseOrderStore
{
    private readonly IGarageContext _context;

    public EfPurchaseOrderStore(IGarageContext context)
    {
        _context = context;
    }

    public bool HasActiveTransaction() => _context.Database.CurrentTransaction != null;

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync();
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await action(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<TResult> ExecuteSerializableReceiveAsync<TResult>(
        long purchaseOrderId,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var tx = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
                : await TryBeginTransactionAsync();
            var committing = false;
            try
            {
                if (_context.Database.IsSqlServer())
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT [Id] FROM [PurchaseOrders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {purchaseOrderId}", ct);
                }

                var result = await action(ct);

                if (tx != null)
                {
                    committing = true;
                    await tx.CommitAsync(ct);
                    committing = false;
                }

                return result;
            }
            catch (Exception ex)
            {
                if (tx != null)
                {
                    try { await tx.RollbackAsync(CancellationToken.None); } catch { }
                }
                _context.ChangeTracker.Clear();
                if (committing)
                {
                    throw new InvalidOperationException("Không xác định được kết quả nhập kho; kiểm tra phiếu nhập trước khi thử lại.", ex);
                }
                throw;
            }
        });
    }

    public async Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> GetPurchaseOrdersPageAsync(
        long? branchId,
        string? statusGroup,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.PurchaseOrders.AsNoTracking().AsQueryable();

        if (branchId.HasValue)
        {
            query = query.Where(po => po.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(statusGroup))
        {
            switch (statusGroup)
            {
                case "ALL":
                    break;
                case "PENDING":
                    query = query.Where(po => po.Status.ToUpper() == "PENDING" || po.Status.ToUpper() == "PENDING_APPROVAL");
                    break;
                case "APPROVED":
                    query = query.Where(po => po.Status.ToUpper() == "APPROVED" || po.Status.ToUpper() == "PARTIAL_RECEIVED" || po.Status.ToUpper() == "PARTIALLY_RECEIVED");
                    break;
                case "RECEIVED":
                    query = query.Where(po => po.Status.ToUpper() == "COMPLETED" || po.Status.ToUpper() == "RECEIVED");
                    break;
            }
        }
        else if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(po => po.Status.ToUpper() == normalizedStatus);
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= total) return (Array.Empty<PurchaseOrder>(), total);
        var orders = await query
            .Include(po => po.Supplier)
            .Include(po => po.DestinationWarehouse)
            .Include(po => po.ApprovedByStaff)
            .Include(po => po.CreatedByNavigation)
            .Include(po => po.PurchaseOrderItems)
                .ThenInclude(i => i.Product)
            .OrderByDescending(po => po.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(po => po.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(ct);

        return (orders, total);
    }

    public async Task<PurchaseOrder?> GetPurchaseOrderDetailByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Supplier)
            .Include(po => po.DestinationWarehouse)
            .Include(po => po.ApprovedByStaff)
            .Include(po => po.CreatedByNavigation)
            .Include(po => po.PurchaseOrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(po => po.Id == id, ct);
    }

    public Task<bool> SupplierExistsAsync(long supplierId, CancellationToken ct = default)
    {
        return _context.Suppliers.AsNoTracking().AnyAsync(s => s.Id == supplierId, ct);
    }

    public Task<long> GetFirstActiveWarehouseIdAsync(long branchId, CancellationToken ct = default)
    {
        return _context.Warehouses
            .AsNoTracking()
            .Where(w => w.BranchId == branchId && (w.IsActive ?? true))
            .OrderBy(w => w.Id)
            .Select(w => w.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> WarehouseBelongsToBranchAsync(long warehouseId, long branchId, CancellationToken ct = default)
    {
        return _context.Warehouses
            .AsNoTracking()
            .AnyAsync(w => w.Id == warehouseId && w.BranchId == branchId, ct);
    }

    public Task<bool> ProductExistsAsync(long productId, CancellationToken ct = default)
    {
        return _context.Products.AsNoTracking().AnyAsync(p => p.Id == productId, ct);
    }

    public async Task AddPurchaseOrderAndSaveAsync(PurchaseOrder po, CancellationToken ct = default)
    {
        _context.PurchaseOrders.Add(po);
        await _context.SaveChangesAsync(ct);
    }

    public Task<PurchaseOrder?> GetPurchaseOrderWithItemsForUpdateAsync(long id, CancellationToken ct = default)
    {
        return _context.PurchaseOrders
            .Include(x => x.PurchaseOrderItems)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<bool> HasGoodsReceiptsForOrderAsync(long purchaseOrderId, CancellationToken ct = default)
    {
        return _context.GoodsReceiptNotes.AsNoTracking().AnyAsync(g => g.PurchaseOrderId == purchaseOrderId, ct);
    }

    public Task<int> CountGoodsReceiptsForOrderAsync(long purchaseOrderId, CancellationToken ct = default)
    {
        return _context.GoodsReceiptNotes.AsNoTracking().CountAsync(x => x.PurchaseOrderId == purchaseOrderId, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }

    public async Task AddGoodsReceiptNoteAndSaveAsync(GoodsReceiptNote grn, CancellationToken ct = default)
    {
        _context.GoodsReceiptNotes.Add(grn);
        await _context.SaveChangesAsync(ct);
    }

    public Task<MyGarage.Models.Inventory?> GetInventoryAsync(long warehouseId, long productId, CancellationToken ct = default)
    {
        return _context.Inventories.FirstOrDefaultAsync(x =>
            x.WarehouseId == warehouseId &&
            x.ProductId == productId, ct);
    }

    public async Task<decimal> GetProductMinStockLevelAsync(long productId, CancellationToken ct = default)
    {
        var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId, ct);
        return product?.MinStockLevel ?? 0m;
    }

    public void AddInventory(MyGarage.Models.Inventory inventory) => _context.Inventories.Add(inventory);

    public void AddStockTransaction(StockTransaction stockTransaction) => _context.StockTransactions.Add(stockTransaction);

    public void AddGoodsReceiptNoteItems(IEnumerable<GoodsReceiptNoteItem> items) => _context.GoodsReceiptNoteItems.AddRange(items);

    public async Task<IReadOnlyList<long>> GetUnresolvedAlertIdsAsync(
        long warehouseId,
        IReadOnlyCollection<long> productIds,
        CancellationToken ct = default)
    {
        return await _context.StockAlerts
            .Where(x =>
                x.IsResolved == false &&
                x.WarehouseId == warehouseId &&
                productIds.Contains(x.ProductId))
            .Select(x => x.Id)
            .ToListAsync(ct);
    }

    public Task<PurchaseOrder?> GetPurchaseOrderBasicAsync(long id, CancellationToken ct = default)
    {
        return _context.PurchaseOrders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<GoodsReceiptNote>> GetGoodsReceiptsForOrderAsync(long purchaseOrderId, CancellationToken ct = default)
    {
        return await _context.GoodsReceiptNotes
            .AsNoTracking()
            .Include(x => x.ReceivedByStaff)
            .Include(x => x.Items)
            .Where(x => x.PurchaseOrderId == purchaseOrderId)
            .OrderByDescending(x => x.ReceivedAt)
            .ToListAsync(ct);
    }

    public Task<PurchaseOrder?> GetPurchaseOrderForFulfillmentAsync(long id, CancellationToken ct = default)
    {
        return _context.PurchaseOrders
            .AsNoTracking()
            .Include(x => x.PurchaseOrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<PurchaseOrder>> GetPurchaseOrdersForDashboardAsync(
        long? branchId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct = default)
    {
        var query = _context.PurchaseOrders
            .AsNoTracking()
            .Include(x => x.PurchaseOrderItems)
                .ThenInclude(i => i.Product)
            .AsQueryable();

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.OrderDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.OrderDate <= toDate.Value);
        }

        return await query
            .OrderByDescending(x => x.OrderDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<GoodsReceiptNote> Grns, IReadOnlyList<GoodsReceiptNoteItem> Items)> GetGrnKpiDataAsync(
        long? branchId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var grnQuery = _context.GoodsReceiptNotes
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Where(x => x.ReceivedAt >= from && x.ReceivedAt < to);

        if (branchId.HasValue)
        {
            grnQuery = grnQuery.Where(x => x.Warehouse.BranchId == branchId.Value);
        }

        var grns = await grnQuery.ToListAsync(ct);
        var grnIds = grns.Select(x => x.Id).ToList();

        var itemRows = await _context.GoodsReceiptNoteItems
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => grnIds.Contains(x.GoodsReceiptNoteId))
            .ToListAsync(ct);

        return (grns, itemRows);
    }

    public async Task RemovePurchaseOrderAndItemsAsync(PurchaseOrder po, CancellationToken ct = default)
    {
        if (po.PurchaseOrderItems.Count > 0)
        {
            _context.PurchaseOrderItems.RemoveRange(po.PurchaseOrderItems);
        }

        _context.PurchaseOrders.Remove(po);
        await _context.SaveChangesAsync(ct);
    }
}
