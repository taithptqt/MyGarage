using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Authorization;
using MyGarage.Application.Inventory;
using MyGarage.Application.Inventory.DTOs;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class InventoryApplicationService : IInventoryApplicationService
{
    private readonly IInventoryStore _store;
    private readonly IMapper _mapper;
    private readonly IInventoryAuthorizationService _authService;

    public InventoryApplicationService(
        IInventoryStore store,
        IMapper mapper,
        IInventoryAuthorizationService authService)
    {
        _store = store;
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
        var summary = await _store.GetCatalogSummaryAsync(ct);

        if (kind == "products")
        {
            var data = await _store.GetCatalogProductsPageAsync(currentPage, size, ct);
            var items = data.Products.Select(p => new
            {
                item = _mapper.Map<ProductResponseDto>(p),
                categoryName = p.CategoryId.HasValue && data.CategoryNames.TryGetValue(p.CategoryId.Value, out var name) ? name : null,
                thumbnail = data.Thumbnails.GetValueOrDefault(p.Id)
            }).Cast<object>().ToList();

            return new CatalogPageResult(items, summary.ProductCount, summary);
        }

        if (kind == "categories")
        {
            var data = await _store.GetCatalogCategoriesPageAsync(currentPage, size, ct);
            var items = data.Categories.Select(c => new
            {
                item = _mapper.Map<CategoryResponseDto>(c),
                parentName = c.ParentId.HasValue && data.ParentNames.TryGetValue(c.ParentId.Value, out var name) ? name : null,
                productCount = data.ProductCounts.GetValueOrDefault(c.Id)
            }).Cast<object>().ToList();

            return new CatalogPageResult(items, summary.CategoryCount, summary);
        }

        var suppliers = await _store.GetCatalogSuppliersPageAsync(currentPage, size, ct);
        var supplierItems = _mapper.Map<List<SupplierResponseDto>>(suppliers).Cast<object>().ToList();
        return new CatalogPageResult(supplierItems, summary.SupplierCount, summary);
    }

    public async Task ExportProductsCsvAsync(Stream outputStream, CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(outputStream, new UTF8Encoding(true), leaveOpen: true);
        await writer.WriteLineAsync("id,sku,name,unit,category,isActive");

        await foreach (var row in _store.StreamProductsForCsvAsync(ct))
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
        var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 25, 100);
        var data = await _store.GetPartsPageDataAsync(search, current, size, ct);

        var items = data.Products.Select(p => new PartsPageItemDto(
            _mapper.Map<ProductResponseDto>(p),
            p.CategoryId.HasValue ? data.CategoryNames.GetValueOrDefault(p.CategoryId.Value) : null,
            data.ImagesByProduct.GetValueOrDefault(p.Id)
        )).ToList();

        return new PartsPageResult(items, data.TotalCount, current, size);
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

        var scopedBranchId = !actor.IsAdmin && actor.BranchId.HasValue ? actor.BranchId.Value : (long?)null;
        var safeTake = Math.Clamp(take, 1, 500);
        var alerts = await _store.GetStockAlertsAsync(resolved, scopedBranchId, safeTake, ct);

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

        var scopedBranchId = !actor.IsAdmin && actor.BranchId.HasValue ? actor.BranchId.Value : (long?)null;

        await _store.ExecuteInTransactionAsync(async token =>
        {
            await _store.RemoveUnresolvedStockAlertsAsync(scopedBranchId, token);

            var inventoryRows = await _store.GetActiveInventoriesForAlertsAsync(scopedBranchId, token);
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
                await _store.AddStockAlertsAsync(newAlerts, token);
            }
        }, ct);

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

        var alert = await _store.GetStockAlertWithWarehouseAsync(alertId, ct);
        if (alert == null || alert.IsResolved)
        {
            return false;
        }

        if (!actor.IsAdmin && actor.BranchId.HasValue && alert.Warehouse != null && alert.Warehouse.BranchId != actor.BranchId.Value)
        {
            return false;
        }

        if (!await _store.PurchaseOrderExistsAsync(purchaseOrderId, ct))
        {
            return false;
        }

        alert.IsResolved = true;
        alert.ResolvedAt = VietnamTime.Now;
        alert.ResolvedByPurchaseOrderId = purchaseOrderId;

        await _store.SaveChangesAsync(ct);
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

        var effectiveBranchId = branchId.HasValue && branchId.Value > 0
            ? branchId
            : (!actor.IsAdmin && actor.BranchId.HasValue ? actor.BranchId : null);

        var normalizedType = !string.IsNullOrWhiteSpace(type) ? type.Trim().ToUpperInvariant() : null;
        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize, 50, 200);

        var (rows, total) = await _store.GetStockTransactionsPageAsync(
            effectiveBranchId,
            warehouseId,
            productId,
            normalizedType,
            from,
            to,
            search,
            currentPage,
            size,
            ct);

        return new StockTransactionPageResult(rows, total, currentPage, size);
    }
}
