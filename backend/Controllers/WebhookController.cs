using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayOS;
using PayOS.Models.Webhooks;
using PayOS.Models.V2.PaymentRequests;
using MyGarage.Interfaces;
using MyGarage.Services;
using MyGarage.Models;
using MyGarage.Enums;

namespace MyGarage.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhookController(
    [FromKeyedServices("OrderClient")] PayOSClient client,
    IGarageContext db,
    IPaymentConfirmationService paymentConfirmationService,
    IOrderStore orderStore,
    IOrderTransactionStore orderTransactionStore) : ControllerBase
{
    private readonly PayOSClient _client = client;
    private readonly IGarageContext _db = db;
    private readonly IPaymentConfirmationService _paymentConfirmationService = paymentConfirmationService;
    private readonly IOrderStore _orderStore = orderStore;
    private readonly IOrderTransactionStore _orderTransactionStore = orderTransactionStore;

    private async Task UpdateRetailOrderStatusDirectlyAsync(
        long retailOrderId,
        bool isPaid,
        decimal amountPaid = 0,
        string transactionCode = "",
        DateTimeOffset? transactionTime = null,
        string? fallbackEmail = null,
        string? fallbackFullName = null)
    {
        var retailOrder = await _db.RetailOrders.FirstOrDefaultAsync(x => x.Id == retailOrderId);
        if (retailOrder is null)
        {
            return;
        }

        retailOrder.Status = isPaid ? nameof(RetailOrderStatus.CONFIRMED) : nameof(RetailOrderStatus.DRAFT);
        retailOrder.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (isPaid)
        {
            await _paymentConfirmationService.SendRetailOrderSuccessEmailAsync(
                retailOrderId,
                amountPaid,
                transactionCode,
                "COMPLETED",
                transactionTime ?? DateTimeOffset.Now,
                fallbackEmail,
                fallbackFullName);
        }
    }

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
        return DateTimeOffset.Now;
    }

    [HttpPost("payment")]
    public async Task<ActionResult> VerifyPayment(Webhook webhook)
    {
        if (webhook == null)
        {
            return BadRequest("Webhook data is required");
        }

        try
        {
            var webhookData = await _client.Webhooks.VerifyAsync(webhook);
            if (webhookData.OrderCode == 123 && webhookData.Description == "VQRIO123" && webhookData.AccountNumber == "12345678")
            {
                return Ok(new { message = "Webhook processed successfully" });
            }

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
                order.Status = order.AmountRemaining > 0 ? PaymentLinkStatus.Underpaid : PaymentLinkStatus.Paid;
                order.LastTransactionUpdate = DateTimeOffset.Now;

                _orderStore.UpdateOrder(order.Id, order);

                Payment? payment = null;
                if (order.PaymentRecordId.HasValue)
                {
                    payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == order.PaymentRecordId.Value);
                }

                payment ??= await _db.Payments
                    .Where(p => p.TransactionCode == order.PaymentLinkId || p.TransactionCode == webhookData.Reference)
                    .OrderByDescending(p => p.Id)
                    .FirstOrDefaultAsync();

                if (payment is not null)
                {
                    var targetStatus = order.AmountRemaining > 0 ? nameof(PaymentStatusCode.PENDING) : nameof(PaymentStatusCode.COMPLETED);
                    await _paymentConfirmationService.ApplyOutcomeAsync(payment, order.AmountPaid, targetStatus);
                }
                else if (order.RetailOrderId.HasValue)
                {
                    // Không có Payment row (CHK constraint skip) → fallback gửi email qua RetailOrder
                    Console.WriteLine($"[Webhook] No Payment row for RetailOrderId={order.RetailOrderId}. Using direct fallback with BuyerEmail={order.BuyerEmail ?? "(null)"}.");
                    await UpdateRetailOrderStatusDirectlyAsync(
                        order.RetailOrderId.Value,
                        order.AmountRemaining <= 0,
                        order.AmountPaid,
                        webhookData.Reference,
                        ParseWebhookDateTime(webhookData.TransactionDateTime),
                        order.BuyerEmail,
                        order.BuyerName);
                }
                else
                {
                    Console.WriteLine($"[Webhook] No Payment row AND no RetailOrderId for OrderCode={order.OrderCode}. Email will NOT be sent.");
                }
            }
            else
            {
                // ⚡ FALLBACK: In-memory order không tìm thấy (backend vừa restart).
                // Tìm payment PENDING trong DB theo Amount để vẫn trigger email.
                // Lưu ý: cách này chỉ là safety net, luồng chính chạy khi order còn trong memory.
                try
                {
                    var amountPaid = (decimal)webhookData.Amount;
                    var cutoff = DateTime.UtcNow.AddMinutes(-30); // Chỉ xét payment tạo trong 30 phút qua

                    var paymentFromDb = await _db.Payments
                        .Where(p => p.Status == nameof(PaymentStatusCode.PENDING)
                                 && p.Amount == amountPaid
                                 && p.CreatedAt >= cutoff)
                        .OrderByDescending(p => p.Id)
                        .FirstOrDefaultAsync();

                    if (paymentFromDb is not null)
                    {
                        await _paymentConfirmationService.ApplyOutcomeAsync(paymentFromDb, amountPaid, nameof(PaymentStatusCode.COMPLETED));
                    }
                }
                catch
                {
                    // Webhook phải luôn trả 200, swallow lỗi ở đây
                }
            }

            return Ok(new { message = "Webhook processed successfully", orderCode = webhookData.OrderCode });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Webhook processing error: {ex.Message}");
            if (ex.Message.Contains("Invalid signature", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Invalid webhook signature" });
            }
            return Problem(ex.Message);
        }
    }
}

