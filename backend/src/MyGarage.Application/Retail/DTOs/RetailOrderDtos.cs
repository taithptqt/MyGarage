using System;
using System.Collections.Generic;

namespace MyGarage.Application.Retail.DTOs;

public sealed record RetailCheckoutItemInput(
    long ProductId,
    int Quantity
);

public sealed record RetailCheckoutInput(
    long BranchId,
    string? CustomerNotes,
    string? VoucherCode,
    List<RetailCheckoutItemInput> Items
);

public sealed record RetailOrderFilterInput(
    long? BranchId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record RetailCustomerOrderPageInput(
    int Page = 1,
    int PageSize = 10,
    long? AnchorOrderId = null
);

public sealed record RetailOrderItemDto(
    long Id,
    long ProductId,
    string? ProductName,
    decimal Quantity,
    decimal UnitPriceAtTime,
    decimal SubTotal,
    decimal? TaxRate,
    decimal? TaxAmount,
    decimal FinalAmount
);

public sealed record RetailOrderDto(
    long Id,
    string OrderCode,
    long BranchId,
    long? CustomerId,
    decimal SubTotal,
    decimal? TierDiscountAmount,
    long? VoucherId,
    decimal? VoucherDiscountAmount,
    decimal? TotalDiscountAmount,
    decimal? TaxAmount,
    decimal FinalAmount,
    string? Status,
    string? CustomerNotes,
    DateTime? CreatedAt,
    List<RetailOrderItemDto> Items
);

public sealed record RetailCustomerOrderPageResultDto(
    List<RetailOrderDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    int DraftCount,
    int ConfirmedCount,
    decimal TotalValue
);
