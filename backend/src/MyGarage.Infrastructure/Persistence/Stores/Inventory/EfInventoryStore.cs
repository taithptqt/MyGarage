using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Inventory;
using MyGarage.Application.Inventory.DTOs;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Inventory;

public sealed partial class EfInventoryStore : IInventoryStore
{
    private readonly IGarageContext _db;

    public EfInventoryStore(IGarageContext db)
    {
        _db = db;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_db.Database.CurrentTransaction != null)
        {
            return null;
        }

        try
        {
            return await _db.Database.BeginTransactionAsync();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("SqlServerRetryingExecutionStrategy"))
            {
                return null;
            }
            throw;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (_db.Database.CurrentTransaction != null)
        {
            await action(ct);
            return;
        }
        var strategy = _db.Database.CreateExecutionStrategy();
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

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        if (_db.Database.CurrentTransaction != null)
        {
            return await action(ct);
        }
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                var result = await action(ct);
                if (tx != null) await tx.CommitAsync(ct);
                return result;
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<CatalogSummaryDto> GetCatalogSummaryAsync(CancellationToken ct = default)
    {
        var productQuery = _db.Products.AsNoTracking().Where(p => p.IsDeleted != true);
        var productCount = await productQuery.CountAsync(ct);
        var classified = await productQuery.CountAsync(p => p.CategoryId != null, ct);
        var categoryCount = await _db.Categories.AsNoTracking().CountAsync(ct);
        var supplierCount = await _db.Suppliers.AsNoTracking().CountAsync(ct);

        return new CatalogSummaryDto(productCount, classified, productCount - classified, categoryCount, supplierCount);
    }

    public async Task<CatalogProductsPageData> GetCatalogProductsPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var productQuery = _db.Products.AsNoTracking().Where(p => p.IsDeleted != true);
        var total = await productQuery.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;

        var rows = offset >= total
            ? new List<Product>()
            : await productQuery.OrderBy(p => p.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);

        var ids = rows.Select(p => p.Id).ToArray();
        var categoryIds = rows.Where(p => p.CategoryId.HasValue).Select(p => p.CategoryId!.Value).Distinct().ToArray();
        var categoryNames = await _db.Categories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.CategoryName, ct);

        var images = await _db.ProductImages.AsNoTracking()
            .Where(i => ids.Contains(i.ProductId))
            .OrderBy(i => i.Id)
            .Select(i => new { i.ProductId, i.ImageUrl, i.IsMain })
            .ToListAsync(ct);

        var thumbs = new Dictionary<long, string>();
        foreach (var image in images)
        {
            if (!string.IsNullOrWhiteSpace(image.ImageUrl) && (!thumbs.ContainsKey(image.ProductId) || image.IsMain == true))
            {
                thumbs[image.ProductId] = image.ImageUrl;
            }
        }

        return new CatalogProductsPageData(rows, categoryNames, thumbs);
    }

    public async Task<CatalogCategoriesPageData> GetCatalogCategoriesPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _db.Categories.AsNoTracking().CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;

        var rows = offset >= total
            ? new List<Category>()
            : await _db.Categories.AsNoTracking().OrderBy(c => c.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);

        var ids = rows.Select(c => c.Id).ToArray();
        var parentIds = rows.Where(c => c.ParentId.HasValue).Select(c => c.ParentId!.Value).Distinct().ToArray();
        var parentNames = await _db.Categories.AsNoTracking()
            .Where(c => parentIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.CategoryName, ct);

        var counts = await _db.Products.AsNoTracking()
            .Where(p => p.IsDeleted != true && p.CategoryId.HasValue && ids.Contains(p.CategoryId.Value))
            .GroupBy(p => p.CategoryId!.Value)
            .Select(g => new { id = g.Key, count = g.Count() })
            .ToListAsync(ct);

        var countById = counts.ToDictionary(c => c.id, c => c.count);
        return new CatalogCategoriesPageData(rows, parentNames, countById);
    }

    public async Task<IReadOnlyList<Supplier>> GetCatalogSuppliersPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _db.Suppliers.AsNoTracking().CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;

        return offset >= total
            ? Array.Empty<Supplier>()
            : await _db.Suppliers.AsNoTracking().OrderBy(s => s.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);
    }

    public async IAsyncEnumerable<ProductCsvRow> StreamProductsForCsvAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var query = _db.Products.AsNoTracking()
            .Where(p => p.IsDeleted != true)
            .OrderBy(p => p.Id)
            .Select(p => new ProductCsvRow(
                p.Id,
                p.Sku,
                p.Name,
                p.Unit,
                p.Category != null ? p.Category.CategoryName : "",
                p.IsActive
            ));

        await foreach (var row in query.AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return row;
        }
    }

    public async Task<PartsPageData> GetPartsPageDataAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Products.AsNoTracking().Where(p => p.IsDeleted != true);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(p => (p.Name ?? "").ToLower().Contains(term) || (p.Sku ?? "").ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;

        var products = offset >= total
            ? new List<Product>()
            : await query.OrderBy(p => p.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);

        var ids = products.Select(p => p.Id).ToArray();
        var categoryIds = products.Where(p => p.CategoryId.HasValue).Select(p => p.CategoryId!.Value).Distinct().ToArray();
        var names = await _db.Categories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.CategoryName, ct);

        var images = await _db.ProductImages.AsNoTracking()
            .Where(i => ids.Contains(i.ProductId))
            .OrderByDescending(i => i.IsMain == true)
            .ThenBy(i => i.SortOrder ?? 0)
            .ThenBy(i => i.Id)
            .Select(i => new { i.ProductId, i.ImageUrl })
            .ToListAsync(ct);

        var imagesByProduct = images.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.First().ImageUrl);
        return new PartsPageData(products, total, names, imagesByProduct);
    }

    public async Task<IReadOnlyList<StockAlert>> GetStockAlertsAsync(bool? resolved, long? branchId, int take, CancellationToken ct = default)
    {
        var query = _db.StockAlerts.AsNoTracking()
            .Include(a => a.Product)
            .Include(a => a.Warehouse)
            .AsQueryable();

        if (resolved.HasValue)
        {
            query = query.Where(a => a.IsResolved == resolved.Value);
        }

        if (branchId.HasValue)
        {
            query = query.Where(a => a.Warehouse != null && a.Warehouse.BranchId == branchId.Value);
        }

        return await query.OrderByDescending(a => a.Id).Take(take).ToListAsync(ct);
    }

    public async Task RemoveUnresolvedStockAlertsAsync(long? branchId, CancellationToken ct = default)
    {
        var alertsToDeleteQuery = _db.StockAlerts.Where(a => !a.IsResolved);
        if (branchId.HasValue)
        {
            alertsToDeleteQuery = alertsToDeleteQuery.Where(a => a.Warehouse != null && a.Warehouse.BranchId == branchId.Value);
        }

        var toDelete = await alertsToDeleteQuery.ToListAsync(ct);
        _db.StockAlerts.RemoveRange(toDelete);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MyGarage.Models.Inventory>> GetActiveInventoriesForAlertsAsync(long? branchId, CancellationToken ct = default)
    {
        var inventoryQuery = _db.Inventories
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
            .Where(i => i.Warehouse != null && i.Warehouse.IsActive != false && i.Product != null && i.Product.IsActive != false && i.Product.IsDeleted != true);

        if (branchId.HasValue)
        {
            inventoryQuery = inventoryQuery.Where(i => i.Warehouse!.BranchId == branchId.Value);
        }

        return await inventoryQuery.ToListAsync(ct);
    }

    public async Task AddStockAlertsAsync(IReadOnlyList<StockAlert> alerts, CancellationToken ct = default)
    {
        await _db.StockAlerts.AddRangeAsync(alerts, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<StockAlert?> GetStockAlertWithWarehouseAsync(long alertId, CancellationToken ct = default)
    {
        return await _db.StockAlerts
            .Include(a => a.Warehouse)
            .FirstOrDefaultAsync(a => a.Id == alertId, ct);
    }

    public async Task<bool> PurchaseOrderExistsAsync(long purchaseOrderId, CancellationToken ct = default)
    {
        return await _db.PurchaseOrders.AnyAsync(po => po.Id == purchaseOrderId, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _db.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<StockTransactionItemDto> Items, int TotalCount)> GetStockTransactionsPageAsync(
        long? branchId,
        long? warehouseId,
        long? productId,
        string? normalizedType,
        DateTime? from,
        DateTime? to,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var joinedQuery = _db.StockTransactions.AsNoTracking()
            .Join(_db.Warehouses.AsNoTracking(), t => t.WarehouseId, w => w.Id, (t, w) => new { t, w });

        if (branchId.HasValue && branchId.Value > 0)
        {
            joinedQuery = joinedQuery.Where(x => x.w.BranchId == branchId.Value);
        }

        if (warehouseId.HasValue && warehouseId.Value > 0)
        {
            joinedQuery = joinedQuery.Where(x => x.t.WarehouseId == warehouseId.Value);
        }

        if (productId.HasValue && productId.Value > 0)
        {
            joinedQuery = joinedQuery.Where(x => x.t.ProductId == productId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedType))
        {
            joinedQuery = joinedQuery.Where(x => x.t.Type == normalizedType);
        }

        if (from.HasValue)
        {
            joinedQuery = joinedQuery.Where(x => x.t.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            joinedQuery = joinedQuery.Where(x => x.t.CreatedAt <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            joinedQuery = joinedQuery.Where(x =>
                (x.t.Product != null && ((x.t.Product.Name ?? "").ToLower().Contains(term) || (x.t.Product.Sku ?? "").ToLower().Contains(term))) ||
                (x.t.Note ?? "").ToLower().Contains(term));
        }

        var total = await joinedQuery.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;

        var rows = offset >= total
            ? Array.Empty<StockTransactionItemDto>()
            : (IReadOnlyList<StockTransactionItemDto>)await joinedQuery
                .OrderByDescending(x => x.t.CreatedAt)
                .Skip((int)offset)
                .Take(pageSize)
                .Select(x => new StockTransactionItemDto(
                    x.t.Id,
                    x.t.ProductId,
                    x.t.Product.Name,
                    x.t.Product.Sku,
                    x.t.WarehouseId,
                    x.w.Name,
                    x.t.Type,
                    x.t.Quantity,
                    x.t.Price ?? 0m,
                    x.t.Note,
                    x.t.CreatedAt ?? DateTime.MinValue,
                    x.t.CreatedByNavigation != null ? x.t.CreatedByNavigation.FullName : null
                ))
                .ToListAsync(ct);

        return (rows, total);
    }
}
