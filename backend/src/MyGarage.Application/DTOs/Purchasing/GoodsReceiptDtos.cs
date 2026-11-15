using System;
using System.Collections.Generic;

namespace MyGarage.Application.Purchasing.DTOs;

public sealed record GoodsReceiptItemDetailDto(
    long Id,
    long PurchaseOrderItemId,
    long ProductId,
    string? ProductSku,
    string? ProductName,
    string? Unit,
    decimal OrderedQuantity,
    decimal ReceivedQuantity,
    decimal UnitCost,
    decimal LineAmount
);

public sealed record GoodsReceiptDetailDto(
    long Id,
    string Code,
    string GrnCode,
    DateTime ReceivedAt,
    DateTime? CreatedAt,
    long PurchaseOrderId,
    string PurchaseOrderCode,
    long WarehouseId,
    string? WarehouseName,
    long? BranchId,
    long? ReceivedBy,
    string? ReceiverName,
    string Status,
    string? Note,
    decimal TotalAmount,
    IReadOnlyList<GoodsReceiptItemDetailDto> Items
);

public sealed record GoodsReceiptPagedResultDto(
    IReadOnlyList<GoodsReceiptDetailDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);

public sealed record GoodsReceiptFilterDto(
    long? BranchId = null,
    long? PurchaseOrderId = null,
    int? Page = null,
    int? PageSize = null,
    bool Envelope = false
);

public sealed record GoodsReceiptCreateItemInput(
    long? PoItemId = null,
    long? PurchaseOrderItemId = null,
    long? ProductId = null,
    decimal? ReceivedQty = null,
    decimal? ReceivedQuantity = null,
    decimal? UnitPrice = null,
    decimal? CostPrice = null
);

public sealed record GoodsReceiptCreateInput(
    long PurchaseOrderId,
    long? SupplierId = null,
    string? Notes = null,
    string? Note = null,
    long? BranchId = null,
    long? ReceivedBy = null,
    DateTime? ReceivedAt = null,
    IReadOnlyList<GoodsReceiptCreateItemInput>? Items = null
);
