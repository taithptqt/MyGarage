using System;
using System.Collections.Generic;

namespace MyGarage.Application.Inventory.DTOs;

public sealed record CatalogSummaryDto(
    int ProductCount,
    int Classified,
    int Unclassified,
    int CategoryCount,
    int SupplierCount
);

public sealed record CatalogPageResult(
    IReadOnlyList<object> Items,
    int Total,
    CatalogSummaryDto Summary
);

public sealed record PartsPageItemDto(
    object Item,
    string? CategoryName,
    string? ImageUrl
);

public sealed record PartsPageResult(
    IReadOnlyList<PartsPageItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

public sealed record StockAlertItemDto(
    long Id,
    long ProductId,
    string? ProductName,
    string? Sku,
    long WarehouseId,
    string? WarehouseName,
    string AlertType,
    decimal CurrentStock,
    decimal MinStock,
    string Severity,
    bool IsResolved,
    DateTime? ResolvedAt,
    long? ResolvedByPurchaseOrderId
);

public sealed record StockTransactionItemDto(
    long Id,
    long ProductId,
    string? ProductName,
    string? Sku,
    long WarehouseId,
    string? WarehouseName,
    string Type,
    decimal Quantity,
    decimal Price,
    string? Note,
    DateTime CreatedAt,
    string? CreatedByName
);

public sealed record StockTransactionPageResult(
    IReadOnlyList<StockTransactionItemDto> Items,
    int Total,
    int Page,
    int PageSize
);
