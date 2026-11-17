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

        if (payment is not null) return payment;

        var linkId = string.IsNullOrWhiteSpace(paymentLinkId) ? null : paymentLinkId.Trim();
        var transactionReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (linkId is null && transactionReference is null) return null;

        var matches = await db.Payments
            .Where(p => (linkId != null && p.TransactionCode == linkId) ||
                        (transactionReference != null && p.TransactionCode == transactionReference))
            .Take(2)
            .ToListAsync(ct);
        if (matches.Count > 1)
            throw new InvalidOperationException("Mã giao dịch không xác định duy nhất khoản thanh toán.");
        return matches.SingleOrDefault();
    }

    public Task EnsureRetailOrderStockDeductedAsync(long retailOrderId, CancellationToken ct = default)
        => RetailOrderStockCommitter.EnsureStockDeductedAsync(db, retailOrderId);

    public async Task<Payment?> FindRecoverablePaymentByLinkAsync(
        string? linkId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(linkId)) return null;

        var matches = await db.Payments
            .Where(p => (p.Status == nameof(PaymentStatusCode.PENDING) || p.Status == nameof(PaymentStatusCode.COMPLETED)) &&
                        p.TransactionCode == linkId)
            .Take(2)
            .ToListAsync(ct);
        if (matches.Count > 1)
            throw new InvalidOperationException("Mã liên kết thanh toán không xác định duy nhất giao dịch.");
        return matches.SingleOrDefault();
    }
}
