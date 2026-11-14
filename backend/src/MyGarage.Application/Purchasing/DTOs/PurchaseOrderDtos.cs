using System;
using System.Collections.Generic;

namespace MyGarage.Application.Purchasing.DTOs;

public sealed record PurchaseOrderItemDetailDto(
    long Id,
    long PurchaseOrderId,
    long ProductId,
    string? Sku,
    string? ProductName,
    string? Unit,
    decimal Qty,
    decimal OrderedQuantity,
    decimal ReceivedQuantity,
    decimal RemainingQuantity,
    decimal UnitPrice,
    decimal CostPrice
);

public sealed record PurchaseOrderDetailDto(
    long Id,
    string Code,
    long BranchId,
    long DestinationWarehouseId,
    string? WarehouseName,
    long SupplierId,
    string? SupplierName,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    decimal TotalAmount,
    string Status,
    long? ApprovedByStaffId,
    string? ApprovedByName,
    long? CreatedBy,
    string? CreatedByName,
    DateTime? CreatedAt,
    IReadOnlyList<PurchaseOrderItemDetailDto> Items
);

public sealed record PurchaseOrderPagedResultDto(
    IReadOnlyList<PurchaseOrderDetailDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);

public sealed record PurchaseOrderFilterDto(
    long? BranchId = null,
    string? Status = null,
    string? StatusGroup = null,
    int? Page = null,
    int? PageSize = null,
    bool Envelope = false
);

public sealed record PurchaseOrderCreateItemInput(
    long ProductId,
    decimal? OrderedQuantity,
    decimal? Qty,
    decimal? CostPrice,
    decimal? UnitPrice
);

public sealed record PurchaseOrderCreateInput(
    long BranchId,
    long DestinationWarehouseId,
    long SupplierId,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    DateOnly? ExpectedDate,
    string? Notes,
    decimal? TotalAmount,
    string? Status,
    long? ApprovedByStaffId,
    long? CreatedBy,
    IReadOnlyList<PurchaseOrderCreateItemInput>? Items
);

public sealed record PurchaseOrderUpdateInput(
    long BranchId,
    long DestinationWarehouseId,
    long SupplierId,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    decimal? TotalAmount,
    string? Status,
    long? ApprovedByStaffId,
    long? CreatedBy
);

public sealed record PurchaseOrderApproveInput(
    long? ApprovedBy,
    string? Note
);

public sealed record PurchaseOrderReceiveItemInput(
    long? PurchaseOrderItemId,
    long? ProductId,
    decimal ReceivedQuantity,
    decimal? CostPrice
);

public sealed record PurchaseOrderReceiveInput(
    DateTime? ReceivedAt,
    string? Note,
    long? ReceivedByStaffId,
    IReadOnlyList<PurchaseOrderReceiveItemInput> Items
);

public sealed record PurchaseOrderReceiveLineResultDto(
    long PurchaseOrderItemId,
    long ProductId,
    decimal AcceptedQuantity,
    decimal OrderedQuantity,
    decimal? TotalReceivedQuantity
);

public sealed record PurchaseOrderReceiveResultDto(
    string GoodsReceiptNoteCode,
    long GoodsReceiptNoteId,
    long PurchaseOrderId,
    string Status,
    DateTime ReceivedAt,
    long DestinationWarehouseId,
    decimal TotalReceivedAmount,
    IReadOnlyList<PurchaseOrderReceiveLineResultDto> Lines
);

public sealed record GoodsReceiptNoteSummaryDto(
    long Id,
    string Code,
    DateTime ReceivedAt,
    string? ReceivedByName,
    string Status,
    decimal TotalAmount,
    string? Note
);

public sealed record PurchaseOrderReceiveHistoryDto(
    long PurchaseOrderId,
    string PurchaseOrderCode,
    IReadOnlyList<GoodsReceiptNoteSummaryDto> Receipts
);

public sealed record PurchaseOrderFulfillmentLineDto(
    long ItemId,
    long ProductId,
    string? ProductName,
    string? ProductSku,
    decimal OrderedQuantity,
    decimal ReceivedQuantity,
    decimal RemainingQuantity,
    decimal FulfillmentPercentage,
    string LineStatus
);

public sealed record PurchaseOrderFulfillmentDto(
    long PurchaseOrderId,
    string PurchaseOrderCode,
    string Status,
    decimal TotalOrderedQuantity,
    decimal TotalReceivedQuantity,
    decimal TotalRemainingQuantity,
    decimal FulfillmentPercentage,
    IReadOnlyList<PurchaseOrderFulfillmentLineDto> Lines
);

public sealed record PurchaseOrderFulfillmentDashboardDto(
    int TotalOrders,
    int CompletedOrders,
    int PartialOrders,
    int PendingOrders,
    decimal OverallFulfillmentRate,
    IReadOnlyList<PurchaseOrderFulfillmentDto> Orders
);

public sealed record PurchaseOrderFulfillmentFilterDto(
    long? BranchId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null
);

public sealed record GrnKpiFilterDto(
    long? BranchId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null
);

public sealed record GrnDailyBreakdownDto(
    string Date,
    int GrnCount,
    decimal TotalValue,
    decimal TotalQuantity
);

public sealed record GrnTopItemDto(
    long ProductId,
    string ProductName,
    string ProductSku,
    decimal TotalQuantityReceived,
    decimal TotalValue
);

public sealed record GrnKpisDto(
    int TotalGrnCount,
    decimal TotalReceivedValue,
    decimal TotalReceivedQuantity,
    IReadOnlyList<GrnDailyBreakdownDto> DailyBreakdown,
    IReadOnlyList<GrnTopItemDto> TopReceivedItems
);
