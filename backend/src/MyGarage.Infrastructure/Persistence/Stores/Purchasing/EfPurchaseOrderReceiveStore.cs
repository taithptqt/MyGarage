using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Interfaces.Stores.Purchasing;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Purchasing;

public sealed class EfPurchaseOrderReceiveStore : IPurchaseOrderReceiveStore
{
    private readonly IGarageContext _context;

    public EfPurchaseOrderReceiveStore(IGarageContext context)
    {
        _context = context;
    }

    public Task<PurchaseOrder?> GetPurchaseOrderWithItemsAsync(long purchaseOrderId)
    {
        return _context.PurchaseOrders
            .Include(x => x.PurchaseOrderItems)
            .FirstOrDefaultAsync(x => x.Id == purchaseOrderId);
    }

    public Task<bool> WarehouseExistsAsync(long warehouseId)
    {
        return _context.Warehouses
            .AsNoTracking()
            .AnyAsync(x => x.Id == warehouseId);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await TryBeginTransactionAsync();
            try
            {
                var result = await action();
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return result;
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }

                throw;
            }
        });
    }

    public async Task AddGoodsReceiptNoteAndSaveAsync(GoodsReceiptNote grn)
    {
        _context.GoodsReceiptNotes.Add(grn);
        await SaveChangesInTransactionAsync();
    }

    public Task<MyGarage.Models.Inventory?> GetInventoryAsync(long warehouseId, long productId)
    {
        return _context.Inventories
            .FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.ProductId == productId);
    }

    public async Task<decimal> GetProductMinStockLevelAsync(long productId)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == productId);
        return product?.MinStockLevel ?? 0m;
    }

    public void AddInventory(MyGarage.Models.Inventory inventory)
    {
        _context.Inventories.Add(inventory);
    }

    public void AddStockTransaction(StockTransaction stockTransaction)
    {
        _context.StockTransactions.Add(stockTransaction);
    }

    public void AddGoodsReceiptNoteItems(IEnumerable<GoodsReceiptNoteItem> items)
    {
        _context.GoodsReceiptNoteItems.AddRange(items);
    }

    public Task SaveChangesAsync()
    {
        return SaveChangesInTransactionAsync();
    }

    public Task<List<long>> GetUnresolvedStockAlertIdsAsync(long warehouseId, IReadOnlyCollection<long> productIds)
    {
        return _context.StockAlerts
            .Where(x =>
                x.IsResolved == false &&
                x.WarehouseId == warehouseId &&
                productIds.Contains(x.ProductId))
            .Select(x => x.Id)
            .ToListAsync();
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync();
    }

    private async Task SaveChangesInTransactionAsync()
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await TryBeginTransactionAsync();
            await _context.SaveChangesAsync();
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
        });
    }
}
