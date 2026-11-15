using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record OrderInvoiceScope(
    long? ServiceCustomerId,
    long? RetailCustomerId,
    long? ServiceOrderId,
    long? RetailOrderId);

public sealed record OrderPendingPaymentCreateResult(
    bool Success,
    long? PaymentId,
    bool InflowRequiresInvoiceViolation);

public interface IOrderDataService
{
    Task UpdateRetailOrderStatusDirectlyAsync(
        long retailOrderId,
        bool isPaid,
        CancellationToken ct = default);

    Task EnsureRetailOrderStockDeductedAsync(
        long retailOrderId,
        CancellationToken ct = default);

    Task<long> ResolvePayOsMethodIdAsync(CancellationToken ct = default);

    Task<long?> ResolveInvoiceIdAsync(
        long? invoiceId,
        long? serviceOrderId,
        long? retailOrderId,
        CancellationToken ct = default);

    Task<Payment?> ResolvePaymentAsync(
        long? paymentRecordId,
        string? paymentLinkId,
        CancellationToken ct = default);

    Task<long?> GetRetailOrderCustomerIdAsync(long retailOrderId, CancellationToken ct = default);

    Task<long?> GetServiceOrderCustomerIdAsync(long serviceOrderId, CancellationToken ct = default);

    Task<OrderInvoiceScope?> GetInvoiceScopeAsync(long invoiceId, CancellationToken ct = default);

    Task<OrderPendingPaymentCreateResult> TryCreatePendingPaymentAsync(
        Payment payment,
        CancellationToken ct = default);

    Task<(bool IsValid, int Amount, string? Error)> ResolveAndValidatePaymentAmountAsync(
        long requestedAmount,
        string normalizedPaymentType,
        long? resolvedInvoiceId,
        long? serviceOrderId,
        long? retailOrderId,
        CancellationToken ct = default);
}
