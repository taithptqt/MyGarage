using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfProductQueryService(IGarageContext db) : IProductQueryService
{
    public async Task<(IReadOnlyList<Product> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking()
            .Where(x => x.IsDeleted != true)
            .OrderByDescending(x => x.Id);
        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var paged = offset >= total
            ? new List<Product>()
            : await query.Skip((int)offset).Take(pageSize).ToListAsync(ct);
        return (paged, total);
    }

    public Task<bool> BranchExistsAsync(long branchId, CancellationToken ct = default) =>
        db.Branches.AsNoTracking()
            .AnyAsync(b => b.Id == branchId && b.IsDeleted != true, ct);

    public async Task<ProductRetailCatalogResult> GetRetailCatalogAsync(
        string? search,
        long? categoryId,
        long? branchId,
        string? sort,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var products = db.Products.AsNoTracking().Where(p => p.IsDeleted != true && p.IsActive != false);
        var activeCount = await products.CountAsync(ct);
        if (categoryId.HasValue) products = products.Where(p => p.CategoryId == categoryId);

        var rows = products.Select(p => new
        {
            Product = p,
            Description = p.ProductImages.Where(i => i.Description != null && i.Description.Trim() != "")
                .OrderByDescending(i => i.IsMain == true).ThenBy(i => i.SortOrder ?? 0).ThenBy(i => i.Id)
                .Select(i => i.Description).FirstOrDefault(),
            CategoryName = p.Category != null && p.Category.IsDeleted != true ? p.Category.CategoryName : ""
        });
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            if (db.Database.IsSqlServer())
                rows = rows.Where(x => EF.Functions.Collate((x.Product.Name + " " + x.Product.Sku + " " +
                    (x.Description ?? "") + " " + x.CategoryName), "Latin1_General_100_CI_AI").Contains(term));
            else
                rows = rows.Where(x => (x.Product.Name + " " + x.Product.Sku + " " +
                    (x.Description ?? "") + " " + x.CategoryName).ToLower().Contains(term));
        }

        var total = await rows.CountAsync(ct);
        var inventory = db.Inventories.AsNoTracking().Where(i => i.Warehouse.IsDeleted != true &&
            i.Warehouse.IsActive != false && (!branchId.HasValue || i.Warehouse.BranchId == branchId));
        var withStock = rows.Select(x => new
        {
            x.Product,
            Quantity = inventory.Any(i => i.ProductId == x.Product.Id &&
                ((i.Warehouse.Type ?? "").ToUpper().Contains("SELLABLE") || (i.Warehouse.Type ?? "").ToUpper().Contains("RETAIL")))
                ? inventory.Where(i => i.ProductId == x.Product.Id &&
                    ((i.Warehouse.Type ?? "").ToUpper().Contains("SELLABLE") || (i.Warehouse.Type ?? "").ToUpper().Contains("RETAIL")))
                    .Sum(i => (decimal?)(i.Quantity - (i.ReservedQuantity ?? 0m))) ?? 0m
                : inventory.Where(i => i.ProductId == x.Product.Id &&
                    !(i.Warehouse.Type ?? "").ToUpper().Contains("DEFECTIVE") && !(i.Warehouse.Type ?? "").ToUpper().Contains("TRANSIT"))
                    .Sum(i => (decimal?)(i.Quantity - (i.ReservedQuantity ?? 0m))) ?? 0m
        });
        var ordered = withStock.OrderByDescending(x => x.Quantity >= 1m);
        ordered = sort switch
        {
            "price-asc" => ordered.ThenBy(x => x.Product.RetailPrice ?? 0m),
            "price-desc" => ordered.ThenByDescending(x => x.Product.RetailPrice ?? 0m),
            "name-desc" => db.Database.IsSqlServer()
                ? ordered.ThenByDescending(x => EF.Functions.Collate(x.Product.Name, "Vietnamese_100_CI_AS"))
                : ordered.ThenByDescending(x => x.Product.Name),
            _ => db.Database.IsSqlServer()
                ? ordered.ThenBy(x => EF.Functions.Collate(x.Product.Name, "Vietnamese_100_CI_AS"))
                : ordered.ThenBy(x => x.Product.Name)
        };
        var offset = ((long)page - 1) * pageSize;
        var pageRows = offset >= total
            ? new List<Product>()
            : await ordered.ThenBy(x => x.Product.Id)
                .Skip((int)offset).Take(pageSize).Select(x => x.Product).ToListAsync(ct);
        var ids = pageRows.Select(p => p.Id).ToList();
        var images = await db.ProductImages.AsNoTracking().Where(i => ids.Contains(i.ProductId)).ToListAsync(ct);
        var availability = await RetailStockAvailabilityResolver.GetAvailableQuantitiesAsync(db, ids, branchId, ct);

        return new ProductRetailCatalogResult(pageRows, total, activeCount, images, availability);
    }

    public Task<IReadOnlyList<RetailStockAvailabilityEntry>> GetStockAvailabilityAsync(
        IReadOnlyList<long> productIds,
        long? branchId,
        CancellationToken ct = default) =>
        RetailStockAvailabilityResolver.GetAvailabilityEntriesAsync(db, productIds, branchId, ct);

    public async Task<IReadOnlyList<LowStockProductRow>> GetLowStockProductsAsync(
        long? branchId,
        int take,
        CancellationToken ct = default)
    {
        var rows = await db.Inventories
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
                .ThenInclude(x => x.Branch)
            .Where(x =>
                x.Product.IsDeleted != true &&
                x.Product.IsActive != false &&
                x.Warehouse.IsDeleted != true &&
                x.Warehouse.IsActive != false &&
                (!branchId.HasValue || x.Warehouse.BranchId == branchId.Value))
            .ToListAsync(ct);

        return rows
            .Select(x =>
            {
                var minStock = Math.Max(0m, x.MinStockLevel ?? x.Product.MinStockLevel ?? 0m);
                var reorderPoint = Math.Max(0m, x.Product.ReorderPoint ?? minStock);
                var quantity = x.Quantity;
                var status = quantity <= 0m
                    ? "OutOfStock"
                    : minStock > 0m && quantity <= minStock
                        ? "BelowMinStock"
                        : reorderPoint > 0m && quantity <= reorderPoint
                            ? "BelowReorder"
                            : string.Empty;

                return new LowStockProductRow(
                    x.ProductId,
                    x.Product.Sku,
                    x.Product.Name,
                    x.WarehouseId,
                    x.Warehouse.Name,
                    x.Warehouse.BranchId,
                    x.Warehouse.Branch.Name,
                    quantity,
                    minStock,
                    reorderPoint,
                    status,
                    Math.Max(0m, minStock - quantity));
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.status))
            .OrderByDescending(x => x.status == "OutOfStock")
            .ThenByDescending(x => x.deficit)
            .ThenBy(x => x.productName)
            .Take(take)
            .ToList();
    }

    public Task<bool> ActiveProductExistsAsync(long id, CancellationToken ct = default) =>
        db.Products.AsNoTracking()
            .AnyAsync(p => p.Id == id && p.IsDeleted != true && p.IsActive != false, ct);

    public Task<IReadOnlyList<RetailBranchAvailabilityRow>> GetRetailStockBreakdownByProductAsync(
        long productId,
        CancellationToken ct = default) =>
        RetailStockAvailabilityResolver.GetRetailStockBreakdownByProductAsync(db, productId, ct);
}
