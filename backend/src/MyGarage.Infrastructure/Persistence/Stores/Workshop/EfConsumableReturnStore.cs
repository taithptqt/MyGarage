using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed class EfConsumableReturnStore : IConsumableReturnStore
{
    private readonly IGarageContext _context;

    public EfConsumableReturnStore(IGarageContext context)
    {
        _context = context;
    }

    public Task<bool> ServiceOrderExistsAsync(long serviceOrderId)
    {
        return _context.ServiceOrders.AsNoTracking()
            .AnyAsync(x => x.Id == serviceOrderId);
    }

    public Task<bool> TechnicianExistsAsync(long technicianId)
    {
        return _context.Staffs.AsNoTracking()
            .AnyAsync(x => x.Id == technicianId && x.IsDeleted != true);
    }

    public void AddBundle(ConsumableReturnBundle bundle)
    {
        _context.ConsumableReturnBundles.Add(bundle);
    }

    public Task<Dictionary<long, ConsumableProductInfo>> GetProductsByIdsAsync(IReadOnlyCollection<long> productIds)
    {
        return _context.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .Select(x => new ConsumableProductInfo(x.Id, x.Unit, x.IsConsumable))
            .ToDictionaryAsync(x => x.Id);
    }

    public void AddBundleItem(ConsumableReturnBundleItem item)
    {
        _context.ConsumableReturnBundleItems.Add(item);
    }

    public Task<MaterialRequestDetail?> GetMaterialRequestDetailForUpdateAsync(long materialReqDetailId)
    {
        return _context.MaterialRequestDetails
            .FirstOrDefaultAsync(x => x.Id == materialReqDetailId);
    }

    public Task<MyGarage.Models.Inventory?> GetInventoryByProductForUpdateAsync(long productId)
    {
        return _context.Inventories
            .FirstOrDefaultAsync(x => x.ProductId == productId);
    }

    public void AddStockTransaction(StockTransaction transaction)
    {
        _context.StockTransactions.Add(transaction);
    }

    public async Task<IReadOnlyList<ConsumableReturnBundleItem>> GetBundleItemsAsync(long bundleId)
    {
        return await _context.ConsumableReturnBundleItems.AsNoTracking()
            .Where(x => x.BundleId == bundleId)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    public async Task SaveChangesInTransactionAsync()
    {
        await ExecuteInTransactionAsync(async () =>
        {
            await _context.SaveChangesAsync();
        });
    }

    private async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            await action();
            if (tx != null)
            {
                await tx.CommitAsync();
            }
        });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync();
    }
}
