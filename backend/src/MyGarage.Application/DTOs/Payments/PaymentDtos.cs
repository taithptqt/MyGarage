using System;
using System.Collections.Generic;

namespace MyGarage.Application.Payments.DTOs;

public sealed record PaymentDto(
    long Id,
    long? InvoiceId,
    long? ServiceOrderId,
    long? RetailOrderId,
    string? PayerType,
    long? PayerId,
    long MethodId,
    decimal Amount,
    string? TransactionCode,
    string Type,
    string? Status,
    string Direction,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);

public sealed record PaymentCreateInput(
    long? InvoiceId,
    long? ServiceOrderId,
    long? RetailOrderId,
    string? PayerType,
    long? PayerId,
    long MethodId,
    decimal Amount,
    string? TransactionCode,
    string Type,
    string? Status,
    string? Direction
);

public sealed record PaymentTransitionInput(
    string Status,
    decimal? Amount,
    string? Note
);

public sealed record PayOsCheckoutInput(
    long? ServiceOrderId,
    long? RetailOrderId,
    decimal Amount,
    string ReturnUrl,
    string CancelUrl
);

public sealed record PayOsCheckoutResultDto(
    long OrderCode,
    string PaymentLinkId,
    string CheckoutUrl,
    string QrCode,
    string Status
);
