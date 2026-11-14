using System;
using System.Collections.Generic;

namespace MyGarage.Application.Invoices.DTOs;

public sealed record InvoiceDetailDto(
    long Id,
    long InvoiceId,
    long? ProductId,
    long? StandardServiceId,
    string? ItemType,
    string? ItemName,
    decimal Quantity,
    decimal UnitPrice,
    decimal? SubTotal,
    decimal? TaxRate,
    decimal? TaxAmount,
    decimal? FinalAmount
);

public sealed record InvoiceDto(
    long Id,
    long? ServiceOrderId,
    long? RetailOrderId,
    long? VehicleId,
    string? Phone,
    string? Address,
    string? CompanyName,
    string? TaxCode,
    decimal? SubTotalAmount,
    decimal? TaxAmount,
    decimal? TotalAmount,
    string PayerType,
    long? PayerId,
    decimal? PaidAmount,
    string? PaymentStatus,
    DateTime? IssuedAt,
    DateTime? CreatedAt,
    IReadOnlyList<InvoiceDetailDto> Details
);

public sealed record InvoiceDetailInput(
    long? ProductId,
    long? StandardServiceId,
    string? ItemType,
    string? ItemName,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRatePercent
);

public sealed record InvoiceCreateInput(
    long? ServiceOrderId,
    long? RetailOrderId,
    long? VehicleId,
    string? Phone,
    string? Address,
    string? CompanyName,
    string? TaxCode,
    string PayerType,
    long? PayerId,
    IReadOnlyList<InvoiceDetailInput>? Details
);

public sealed record InvoiceCalculationResultDto(
    long InvoiceId,
    decimal SubTotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingDue,
    string PaymentStatus
);
