using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Authorization;
using MyGarage.Application.Inventory;
using MyGarage.Application.Inventory.DTOs;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class InventoryApplicationService : IInventoryApplicationService
{
    private readonly IGarageContext _db;
    private readonly IMapper _mapper;
    private readonly IInventoryAuthorizationService _authService;

    public InventoryApplicationService(
        IGarageContext db,
        IMapper mapper,
        IInventoryAuthorizationService authService)
    {
        _db = db;
        _mapper = mapper;
        _authService = authService;
    }

    public async Task<CatalogPageResult?> GetCatalogPageAsync(
        string kind,
        int? page,
        int? pageSize,
        CancellationToken ct = default)
    {
        if (kind is not ("products" or "categories" or "suppliers"))
        {
            return null;
        }

        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var productQuery = _db.Products.AsNoTracking().Where(p => p.IsDeleted != true);
        var productCount = await productQuery.CountAsync(ct);
        var classified = await productQuery.CountAsync(p => p.CategoryId != null, ct);
        var categoryCount = await _db.Categories.AsNoTracking().CountAsync(ct);
        var supplierCount = await _db.Suppliers.AsNoTracking().CountAsync(ct);
        var summary = new CatalogSummaryDto(productCount, classified, productCount - classified, categoryCount, supplierCount);
        var offset = ((long)currentPage - 1) * size;

        if (kind == "products")
        {
            var rows = offset >= productCount
                ? new List<Product>()
                : await productQuery.OrderBy(p => p.Id).Skip((int)offset).Take(size).ToListAsync(ct);

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

            var items = rows.Select(p => new
            {
                item = _mapper.Map<ProductResponseDto>(p),
                categoryName = p.CategoryId.HasValue && categoryNames.TryGetValue(p.CategoryId.Value, out var name) ? name : null,
                thumbnail = thumbs.GetValueOrDefault(p.Id)
            }).Cast<object>().ToList();

            return new CatalogPageResult(items, productCount, summary);
        }

        if (kind == "categories")
        {
            var rows = offset >= categoryCount
                ? new List<Category>()
                : await _db.Categories.AsNoTracking().OrderBy(c => c.Id).Skip((int)offset).Take(size).ToListAsync(ct);

            var ids = rows.Select(c => c.Id).ToArray();
            var parentIds = rows.Where(c => c.ParentId.HasValue).Select(c => c.ParentId!.Value).Distinct().ToArray();
            var parentNames = await _db.Categories.AsNoTracking()
                .Where(c => parentIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.CategoryName, ct);

            var counts = await productQuery
                .Where(p => p.CategoryId.HasValue && ids.Contains(p.CategoryId.Value))
                .GroupBy(p => p.CategoryId!.Value)
                .Select(g => new { id = g.Key, count = g.Count() })
                .ToListAsync(ct);

            var countById = counts.ToDictionary(c => c.id, c => c.count);

            var items = rows.Select(c => new
            {
                item = _mapper.Map<CategoryResponseDto>(c),
                parentName = c.ParentId.HasValue && parentNames.TryGetValue(c.ParentId.Value, out var name) ? name : null,
                productCount = countById.GetValueOrDefault(c.Id)
            }).Cast<object>().ToList();

            return new CatalogPageResult(items, categoryCount, summary);
        }

        var suppliers = offset >= supplierCount
            ? new List<Supplier>()
            : await _db.Suppliers.AsNoTracking().OrderBy(s => s.Id).Skip((int)offset).Take(size).ToListAsync(ct);

        var supplierItems = _mapper.Map<List<SupplierResponseDto>>(suppliers).Cast<object>().ToList();
        return new CatalogPageResult(supplierItems, supplierCount, summary);
    }

    public async Task ExportProductsCsvAsync(Stream outputStream, CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(outputStream, new UTF8Encoding(true), leaveOpen: true);
        await writer.WriteLineAsync("id,sku,name,unit,category,isActive");

        var query = _db.Products.AsNoTracking()
            .Where(p => p.IsDeleted != true)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.Sku,
                p.Name,
                p.Unit,
                Category = p.Category != null ? p.Category.CategoryName : "",
                p.IsActive
            });

        await foreach (var row in query.AsAsyncEnumerable().WithCancellation(ct))
        {
            var values = new[]
            {
                row.Id.ToString(),
                row.Sku,
                row.Name,
                row.Unit,
                row.Category,
                row.IsActive?.ToString().ToLowerInvariant() ?? ""
            };
            await writer.WriteLineAsync(string.Join(",", values.Select(CsvEscape)));
        }

        await writer.FlushAsync(ct);
    }

    private static string CsvEscape(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    public async Task<PartsPageResult> GetPartsPageAsync(
        string? search,
        int? page,
        int? pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Products.AsNoTracking().Where(p => p.IsDeleted != true);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(p => (p.Name ?? "").ToLower().Contains(term) || (p.Sku ?? "").ToLower().Contains(term));
        }

        var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 25, 100);
        var total = await query.CountAsync(ct);
        var offset = ((long)current - 1) * size;

        var products = offset >= total
            ? new List<Product>()
            : await query.OrderBy(p => p.Id).Skip((int)offset).Take(size).ToListAsync(ct);

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

        var items = products.Select(p => new PartsPageItemDto(
            _mapper.Map<ProductResponseDto>(p),
            p.CategoryId.HasValue ? names.GetValueOrDefault(p.CategoryId.Value) : null,
            imagesByProduct.GetValueOrDefault(p.Id)
        )).ToList();

        return new PartsPageResult(items, total, current, size);
    }

    public async Task<IReadOnlyList<StockAlertItemDto>> GetStockAlertsAsync(
        bool? resolved,
        int take,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageStockAlertsAsync(actor.BranchId, actor, ct))
        {
            return Array.Empty<StockAlertItemDto>();
        }

        var query = _db.StockAlerts.AsNoTracking()
            .Include(a => a.Product)
            .Include(a => a.Warehouse)
            .AsQueryable();

        if (resolved.HasValue)
        {
            query = query.Where(a => a.IsResolved == resolved.Value);
        }

        if (!actor.IsAdmin && actor.BranchId.HasValue)
        {
            query = query.Where(a => a.Warehouse != null && a.Warehouse.BranchId == actor.BranchId.Value);
        }

        var safeTake = Math.Clamp(take, 1, 500);
        var alerts = await query.OrderByDescending(a => a.Id).Take(safeTake).ToListAsync(ct);

        return alerts.Select(a => new StockAlertItemDto(
            a.Id,
            a.ProductId,
            a.Product?.Name,
            a.Product?.Sku,
            a.WarehouseId,
            a.Warehouse?.Name,
            a.AlertType,
            a.CurrentStock,
            a.MinStock,
            InventoryDomainRules.ResolveAlertSeverity(a.AlertType),
            a.IsResolved,
            a.ResolvedAt,
            a.ResolvedByPurchaseOrderId
        )).ToList();
    }

    public async Task<IReadOnlyList<StockAlertItemDto>> RebuildStockAlertsAsync(
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageStockAlertsAsync(actor.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo lại cảnh báo kho.");
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();

            var alertsToDeleteQuery = _db.StockAlerts.Where(a => !a.IsResolved);
            if (!actor.IsAdmin && actor.BranchId.HasValue)
            {
                alertsToDeleteQuery = alertsToDeleteQuery.Where(a => a.Warehouse != null && a.Warehouse.BranchId == actor.BranchId.Value);
            }

            var toDelete = await alertsToDeleteQuery.ToListAsync(ct);
            _db.StockAlerts.RemoveRange(toDelete);
            await _db.SaveChangesAsync(ct);

            var inventoryQuery = _db.Inventories
                .Include(i => i.Product)
                .Include(i => i.Warehouse)
                .Where(i => i.Warehouse != null && i.Warehouse.IsActive != false && i.Product != null && i.Product.IsActive != false && i.Product.IsDeleted != true);

            if (!actor.IsAdmin && actor.BranchId.HasValue)
            {
                inventoryQuery = inventoryQuery.Where(i => i.Warehouse!.BranchId == actor.BranchId.Value);
            }

            var inventoryRows = await inventoryQuery.ToListAsync(ct);
            var newAlerts = new List<StockAlert>();

            foreach (var inv in inventoryRows)
            {
                var minStock = InventoryDomainRules.ResolveEffectiveMinStock(inv.MinStockLevel, inv.Product?.MinStockLevel);
                var reorderPoint = InventoryDomainRules.ResolveEffectiveReorderPoint(inv.Product?.ReorderPoint, minStock);
                var alertType = InventoryDomainRules.EvaluateStockAlertType(inv.Quantity, minStock, reorderPoint);

                if (!string.IsNullOrEmpty(alertType))
                {
                    newAlerts.Add(new StockAlert
                    {
                        ProductId = inv.ProductId,
                        WarehouseId = inv.WarehouseId,
                        AlertType = alertType,
                        CurrentStock = inv.Quantity,
                        MinStock = minStock,
                        IsResolved = false
                    });
                }
            }

            if (newAlerts.Count > 0)
            {
                await _db.StockAlerts.AddRangeAsync(newAlerts, ct);
                await _db.SaveChangesAsync(ct);
            }

            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        });

        return await GetStockAlertsAsync(false, 200, actor, ct);
    }

    public async Task<bool> ResolveStockAlertAsync(
        long alertId,
        long purchaseOrderId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageStockAlertsAsync(actor.BranchId, actor, ct))
        {
            return false;
        }

        var alert = await _db.StockAlerts.Include(a => a.Warehouse).FirstOrDefaultAsync(a => a.Id == alertId, ct);
        if (alert == null || alert.IsResolved)
        {
            return false;
        }

        if (!actor.IsAdmin && actor.BranchId.HasValue && alert.Warehouse != null && alert.Warehouse.BranchId != actor.BranchId.Value)
        {
            return false;
        }

        var poExists = await _db.PurchaseOrders.AnyAsync(po => po.Id == purchaseOrderId, ct);
        if (!poExists)
        {
            return false;
        }

        alert.IsResolved = true;
        alert.ResolvedAt = VietnamTime.Now;
        alert.ResolvedByPurchaseOrderId = purchaseOrderId;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<StockTransactionPageResult> GetStockTransactionsAsync(
        long? branchId,
        long? warehouseId,
        long? productId,
        string? type,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        string? search,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanViewStockTransactionsAsync(branchId, actor, ct))
        {
            return new StockTransactionPageResult(Array.Empty<StockTransactionItemDto>(), 0, page, pageSize);
        }

        var joinedQuery = _db.StockTransactions.AsNoTracking()
            .Join(_db.Warehouses.AsNoTracking(), t => t.WarehouseId, w => w.Id, (t, w) => new { t, w });

        if (branchId.HasValue && branchId.Value > 0)
        {
            joinedQuery = joinedQuery.Where(x => x.w.BranchId == branchId.Value);
        }
        else if (!actor.IsAdmin && actor.BranchId.HasValue)
        {
            joinedQuery = joinedQuery.Where(x => x.w.BranchId == actor.BranchId.Value);
        }

        if (warehouseId.HasValue && warehouseId.Value > 0)
        {
            joinedQuery = joinedQuery.Where(x => x.t.WarehouseId == warehouseId.Value);
        }

        if (productId.HasValue && productId.Value > 0)
        {
            joinedQuery = joinedQuery.Where(x => x.t.ProductId == productId.Value);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            joinedQuery = joinedQuery.Where(x => x.t.Type == type.Trim().ToUpperInvariant());
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
            joinedQuery = joinedQuery.Where(x => (x.t.Product != null && ((x.t.Product.Name ?? "").ToLower().Contains(term) || (x.t.Product.Sku ?? "").ToLower().Contains(term))) ||
                                                 (x.t.Note ?? "").ToLower().Contains(term));
        }

        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize, 50, 200);
        var total = await joinedQuery.CountAsync(ct);
        var offset = ((long)currentPage - 1) * size;

        var rows = offset >= total
            ? new List<StockTransactionItemDto>()
            : await joinedQuery
                .OrderByDescending(x => x.t.CreatedAt)
                .Skip((int)offset)
                .Take(size)
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

        return new StockTransactionPageResult(rows, total, currentPage, size);
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_db.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _db.Database.BeginTransactionAsync();
    }
}
