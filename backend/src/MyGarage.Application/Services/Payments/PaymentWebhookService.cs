using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Common;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Application.Payments;

public sealed class PaymentWebhookService(
    IPayOsGateway payOsGateway,
    IOrderStore orderStore,
    IOrderTransactionStore orderTransactionStore,
    IWebhookPaymentDataService webhookDataService,
    IPaymentConfirmationService paymentConfirmationService) : IPaymentWebhookService
{
    private const string PaymentStatusPending = nameof(PaymentStatusCode.PENDING);
    private const string PaymentStatusCompleted = nameof(PaymentStatusCode.COMPLETED);

    private readonly IPayOsGateway _payOsGateway = payOsGateway;
    private readonly IOrderStore _orderStore = orderStore;
    private readonly IOrderTransactionStore _orderTransactionStore = orderTransactionStore;
    private readonly IWebhookPaymentDataService _webhookDataService = webhookDataService;
    private readonly IPaymentConfirmationService _paymentConfirmationService = paymentConfirmationService;

    private static DateTimeOffset ParseWebhookDateTime(string dateTimeString)
    {
        // Try to parse the standard ISO format first (2025-10-10T11:13:30+07:00)
        if (DateTimeOffset.TryParse(dateTimeString, out var result))
        {
            return result;
        }

        // Handle webhook format (2023-02-04 18:25:00)
        if (DateTime.TryParseExact(dateTimeString, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var dateTime))
        {
            return new DateTimeOffset(dateTime, TimeSpan.FromHours(7));
        }

        // Fallback to current time if parsing fails
        return VietnamTime.OffsetNow;
    }

    public async Task<WebhookProcessResult> VerifyAndProcessWebhookAsync(JsonElement webhookPayload, CancellationToken ct = default)
    {
        var payload = await _payOsGateway.VerifyWebhookAsync(webhookPayload, ct);
        if (payload.OrderCode == 123 && payload.Description == "VQRIO123" && payload.AccountNumber == "12345678")
        {
            return new WebhookProcessResult(true, null);
        }

        await ProcessVerifiedPaymentAsync(payload, ct);
        return new WebhookProcessResult(false, payload.OrderCode);
    }

    public async Task ProcessVerifiedPaymentAsync(VerifiedPaymentWebhookInput webhookData, CancellationToken ct = default)
    {
        var order = _orderStore.GetOrderByOrderCode(webhookData.OrderCode);
        if (order != null)
        {
            var existingTransactions = _orderTransactionStore.GetTransactionsByOrderId(order.Id);
            var transactionExists = existingTransactions.Any(t => t.Reference == webhookData.Reference);

            if (!transactionExists)
            {
                var transaction = new OrderTransaction
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    PaymentLinkId = order.PaymentLinkId ?? "",
                    Reference = webhookData.Reference,
                    Amount = webhookData.Amount,
                    AccountNumber = webhookData.AccountNumber,
                    Description = webhookData.Description,
                    TransactionDateTime = ParseWebhookDateTime(webhookData.TransactionDateTime),
                    VirtualAccountName = webhookData.VirtualAccountName,
                    VirtualAccountNumber = webhookData.VirtualAccountNumber,
                    CounterAccountBankId = webhookData.CounterAccountBankId,
                    CounterAccountBankName = webhookData.CounterAccountBankName,
                    CounterAccountName = webhookData.CounterAccountName,
                    CounterAccountNumber = webhookData.CounterAccountNumber
                };

                _orderTransactionStore.CreateTransaction(transaction);
            }

            var allTransactions = _orderTransactionStore.GetTransactionsByOrderId(order.Id);
            var totalAmountPaid = allTransactions.Sum(t => t.Amount);

            order.AmountPaid = totalAmountPaid;
            order.AmountRemaining = order.Amount - totalAmountPaid;
            order.Status = order.AmountRemaining > 0 ? PayOsLinkStatus.Underpaid : PayOsLinkStatus.Paid;
            order.LastTransactionUpdate = VietnamTime.OffsetNow;

            _orderStore.UpdateOrder(order.Id, order);

            var payment = await _webhookDataService.FindPaymentForOrderAsync(
                order.PaymentRecordId,
                order.PaymentLinkId,
                webhookData.Reference,
                ct);

            if (payment is not null)
            {
                var targetStatus = order.AmountRemaining > 0 ? PaymentStatusPending : PaymentStatusCompleted;
                await _paymentConfirmationService.ApplyOutcomeAsync(payment, order.AmountPaid, targetStatus);

                if (order.RetailOrderId.HasValue && string.Equals(targetStatus, PaymentStatusCompleted, StringComparison.OrdinalIgnoreCase))
                {
                    await _webhookDataService.EnsureRetailOrderStockDeductedAsync(order.RetailOrderId.Value, ct);
                }
            }
            else if (order.RetailOrderId.HasValue)
            {
                // Không có Payment row (CHK constraint skip) → fallback gửi email qua RetailOrder
                Console.WriteLine($"[Webhook] No Payment row for RetailOrderId={order.RetailOrderId}. Using direct fallback.");
                await _webhookDataService.UpdateRetailOrderStatusDirectlyAsync(
                    order.RetailOrderId.Value,
                    order.AmountRemaining <= 0,
                    order.AmountPaid,
                    webhookData.Reference,
                    ParseWebhookDateTime(webhookData.TransactionDateTime),
                    order.BuyerEmail,
                    order.BuyerName,
                    ct);
            }
            else
            {
                Console.WriteLine($"[Webhook] No Payment row AND no RetailOrderId for OrderCode={order.OrderCode}. Email will NOT be sent.");
            }
        }
        else
        {
            // After a restart, one webhook amount is not necessarily the cumulative amount.
            // Recover only by the verified link id and ask PayOS for its current totals.
            var linkId = webhookData.PaymentLinkId?.Trim();
            if (string.IsNullOrWhiteSpace(linkId))
                throw new InvalidOperationException("Thiếu mã liên kết để đối soát thanh toán.");

            var paymentFromDb = await _webhookDataService.FindRecoverablePaymentByLinkAsync(linkId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy giao dịch thanh toán để đối soát.");
            var snapshot = await _payOsGateway.GetPaymentLinkAsync(linkId, ct);
            if (snapshot.Amount <= 0 || snapshot.AmountPaid < 0 || snapshot.AmountRemaining < 0 ||
                snapshot.AmountPaid + snapshot.AmountRemaining != snapshot.Amount ||
                snapshot.Status is not (PayOsLinkStatus.Paid or PayOsLinkStatus.Underpaid or PayOsLinkStatus.Pending or PayOsLinkStatus.Processing))
                throw new InvalidOperationException("Trạng thái liên kết thanh toán cần được đối soát.");

            var targetStatus = snapshot.Status == PayOsLinkStatus.Paid && snapshot.AmountRemaining == 0
                ? PaymentStatusCompleted : PaymentStatusPending;
            if (snapshot.Status == PayOsLinkStatus.Paid && snapshot.AmountRemaining != 0 ||
                string.Equals(paymentFromDb.Status, PaymentStatusCompleted, StringComparison.OrdinalIgnoreCase) && targetStatus != PaymentStatusCompleted)
                throw new InvalidOperationException("Trạng thái thanh toán không nhất quán.");

            await _paymentConfirmationService.ApplyOutcomeAsync(paymentFromDb, snapshot.AmountPaid, targetStatus);
        }
    }
}
