using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Inventory.DTOs;
using MyGarage.Models;

namespace MyGarage.Application.Inventory;

public interface IInventoryStore :
    IInventoryCatalogStore,
    IInventoryStockAlertStore,
    IInventoryOperationsStore,
    IInventoryAdminStore
{
}

public sealed record CatalogProductsPageData(
    IReadOnlyList<Product> Products,
    IReadOnlyDictionary<long, string> CategoryNames,
    IReadOnlyDictionary<long, string> Thumbnails);

public sealed record CatalogCategoriesPageData(
    IReadOnlyList<Category> Categories,
    IReadOnlyDictionary<long, string> ParentNames,
    IReadOnlyDictionary<long, int> ProductCounts);

public sealed record PartsPageData(
    IReadOnlyList<Product> Products,
    int TotalCount,
    IReadOnlyDictionary<long, string> CategoryNames,
    IReadOnlyDictionary<long, string> ImagesByProduct);

public sealed record ProductCsvRow(
    long Id,
    string? Sku,
    string? Name,
    string? Unit,
    string Category,
    bool? IsActive);

public sealed record BranchSummaryRowData(
    long BranchId,
    string BranchName,
    string? BranchStatus);

public sealed record BranchInventoryAggregateData(
    long BranchId,
    decimal TotalQuantity,
    decimal ReservedQuantity,
    decimal TotalValue,
    int LowStockCount,
    int NegativeStockCount);

public sealed record WarehouseStructureRowData(
    long Id,
    string? Code,
    string? Name,
    string? Type,
    long? BranchId,
    string? BranchName,
    bool? IsActive,
    long? ManagerId);

public sealed record WarehouseBinCountData(
    long WarehouseId,
    string Bin,
    int SkuCount);

public sealed record AdminStockTransactionRowData(
    StockTransaction Transaction,
    Warehouse Warehouse,
    Branch Branch);
