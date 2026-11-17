using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IWebhookPaymentDataService
{
    Task UpdateRetailOrderStatusDirectlyAsync(
        long retailOrderId,
        bool isPaid,
        decimal amountPaid,
        string transactionCode,
        DateTimeOffset transactionTime,
        string? fallbackEmail,
        string? fallbackFullName,
        CancellationToken ct = default);

    Task<Payment?> FindPaymentForOrderAsync(
        long? paymentRecordId,
        string? paymentLinkId,
        string? reference,
        CancellationToken ct = default);

    Task EnsureRetailOrderStockDeductedAsync(long retailOrderId, CancellationToken ct = default);

    Task<Payment?> FindRecoverablePaymentByLinkAsync(
        string? linkId,
        CancellationToken ct = default);
}
