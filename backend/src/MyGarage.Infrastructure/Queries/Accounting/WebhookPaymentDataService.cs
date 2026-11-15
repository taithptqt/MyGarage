using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfWebhookPaymentDataService(
    IGarageContext db,
    IPaymentConfirmationService paymentConfirmationService) : IWebhookPaymentDataService
{
    private const string RetailOrderStatusConfirmed = nameof(RetailOrderStatus.CONFIRMED);
    private const string RetailOrderStatusDraft = nameof(RetailOrderStatus.DRAFT);

    public async Task UpdateRetailOrderStatusDirectlyAsync(
        long retailOrderId,
        bool isPaid,
        decimal amountPaid,
        string transactionCode,
        DateTimeOffset transactionTime,
        string? fallbackEmail,
        string? fallbackFullName,
        CancellationToken ct = default)
    {
        var retailOrder = await db.RetailOrders.FirstOrDefaultAsync(x => x.Id == retailOrderId, ct);
        if (retailOrder is null)
        {
            return;
        }

        retailOrder.Status = isPaid ? RetailOrderStatusConfirmed : RetailOrderStatusDraft;
        retailOrder.UpdatedAt = VietnamTime.Now;
        await db.SaveChangesAsync(ct);

        if (isPaid)
        {
            await RetailOrderStockCommitter.EnsureStockDeductedAsync(db, retailOrderId);

            await paymentConfirmationService.SendRetailOrderSuccessEmailAsync(
                retailOrderId,
                amountPaid,
                transactionCode,
                "COMPLETED",
                transactionTime,
                fallbackEmail,
                fallbackFullName);
        }
    }

    public async Task<Payment?> FindPaymentForOrderAsync(
        long? paymentRecordId,
        string? paymentLinkId,
        string? reference,
        CancellationToken ct = default)
    {
        Payment? payment = null;
        if (paymentRecordId.HasValue)
        {
            payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentRecordId.Value, ct);
        }

        return payment ?? await db.Payments
            .Where(p => p.TransactionCode == paymentLinkId || p.TransactionCode == reference)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task EnsureRetailOrderStockDeductedAsync(long retailOrderId, CancellationToken ct = default)
        => RetailOrderStockCommitter.EnsureStockDeductedAsync(db, retailOrderId);

    public async Task<Payment?> FindPendingPaymentByLinkOrReferenceAsync(
        string? linkId,
        string? reference,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(linkId) && string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        return await db.Payments
            .Where(p => p.Status == nameof(PaymentStatusCode.PENDING) &&
                        ((!string.IsNullOrWhiteSpace(linkId) && p.TransactionCode == linkId) ||
                         (!string.IsNullOrWhiteSpace(reference) && p.TransactionCode == reference)))
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
    }
}
