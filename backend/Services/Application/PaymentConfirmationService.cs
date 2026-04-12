using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public class PaymentConfirmationService : IPaymentConfirmationService
{
    private const string PaymentStatusCompleted = nameof(PaymentStatusCode.COMPLETED);
    private const string PaymentStatusCanceled = nameof(PaymentStatusCode.CANCELED);
    private const string PaymentStatusFailed = nameof(PaymentStatusCode.FAILED);
    private const string PaymentStatusPending = nameof(PaymentStatusCode.PENDING);
    private const string RetailOrderStatusConfirmed = nameof(RetailOrderStatus.CONFIRMED);
    private const string RetailOrderStatusDraft = nameof(RetailOrderStatus.DRAFT);
    private const string DocumentPaymentStatusPaid = nameof(InvoicePaymentStatusCode.PAID);
    private const string DocumentPaymentStatusPartial = nameof(InvoicePaymentStatusCode.PARTIAL);
    private const string DocumentPaymentStatusUnpaid = nameof(InvoicePaymentStatusCode.UNPAID);
    private readonly IGarageContext _db;
    private readonly IEmailService _emailService;
    private readonly IOrderStore _orderStore;
    private readonly ILogger<PaymentConfirmationService> _logger;

    public PaymentConfirmationService(
        IGarageContext db,
        IEmailService emailService,
        IOrderStore orderStore,
        ILogger<PaymentConfirmationService> logger)
    {
        _db = db;
        _emailService = emailService;
        _orderStore = orderStore;
        _logger = logger;
    }

    public async Task ApplyOutcomeAsync(Payment payment, decimal amountPaid, string status)
    {
        var normalizedStatus = NormalizeStatus(status);
        var previousStatus = (payment.Status ?? string.Empty).ToUpperInvariant();

        payment.Amount = amountPaid;
        payment.Status = normalizedStatus;
        payment.UpdatedAt = VietnamTime.Now;
        await _db.SaveChangesAsync();

        await UpdateRelatedDocumentsAsync(payment);

        if (previousStatus != PaymentStatusCompleted && normalizedStatus == PaymentStatusCompleted)
        {
            if (previousStatus != nameof(PaymentStatusCode.COMPLETED))
            {
                // Transition mÃ¡Â»â€ºi tÃ¡Â»Â« non-COMPLETED Ã¢â€ â€™ COMPLETED Ã¢â€ â€™ luÃƒÂ´n gÃ¡Â»Â­i
                _logger.LogInformation(
                    "[PaymentConfirmationService] Transition {PreviousStatus} Ã¢â€ â€™ COMPLETED cho Payment #{PaymentId}. GÃ¡Â»Â­i email.",
                    previousStatus, payment.Id);
                await TrySendPaymentSuccessEmailAsync(payment);
            }
            else
            {
                // previousStatus Ã„â€˜ÃƒÂ£ lÃƒÂ  COMPLETED (webhook chÃ¡ÂºÂ¡y trÃ†Â°Ã¡Â»â€ºc hoÃ¡ÂºÂ·c retry).
                // VÃ¡ÂºÂ«n gÃ¡Â»Â­i email vÃƒÂ¬ webhook chÃ¡Â»â€° set status, khÃƒÂ´ng gÃ¡Â»Â­i email.
                _logger.LogInformation(
                    "[PaymentConfirmationService] Payment #{PaymentId} Ã„â€˜ÃƒÂ£ COMPLETED (cÃƒÂ³ thÃ¡Â»Æ’ do webhook). " +
                    "Retry gÃ¡Â»Â­i email Ã„â€˜Ã¡Â»Æ’ Ã„â€˜Ã¡ÂºÂ£m bÃ¡ÂºÂ£o khÃƒÂ¡ch nhÃ¡ÂºÂ­n Ã„â€˜Ã†Â°Ã¡Â»Â£c hoÃƒÂ¡ Ã„â€˜Ã†Â¡n.",
                    payment.Id);
                await TrySendPaymentSuccessEmailAsync(payment);
            }
        }
    }

    private static string NormalizeStatus(string? status)
    {
        var normalized = status?.Trim().ToUpperInvariant();
        return normalized switch
        {
            PaymentStatusCompleted => PaymentStatusCompleted,
            PaymentStatusCanceled => PaymentStatusCanceled,
            PaymentStatusFailed => PaymentStatusFailed,
            _ => PaymentStatusPending
        };
    }

    private async Task UpdateRelatedDocumentsAsync(Payment payment)
    {
        if (payment.InvoiceId.HasValue)
        {
            var invoice = await _db.Invoices.FirstOrDefaultAsync(x => x.Id == payment.InvoiceId.Value);
            if (invoice is not null)
            {
                var paidAmount = await _db.Payments
                    .Where(x => x.InvoiceId == payment.InvoiceId.Value && x.Status == PaymentStatusCompleted)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                invoice.PaidAmount = paidAmount;
                var invoiceTotal = invoice.TotalAmount ?? 0m;
                invoice.PaymentStatus = ResolveDocumentPaymentStatus(paidAmount, invoiceTotal);
                invoice.UpdatedAt = VietnamTime.Now;
            }
        }

        if (payment.ServiceOrderId.HasValue)
        {
            var serviceOrder = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == payment.ServiceOrderId.Value);
            if (serviceOrder is not null)
            {
                var paidAmount = await _db.Payments
                    .Where(x => x.ServiceOrderId == payment.ServiceOrderId.Value && x.Status == PaymentStatusCompleted)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                var serviceTotal = serviceOrder.FinalAmount ?? 0m;
                serviceOrder.PaymentStatus = ResolveDocumentPaymentStatus(paidAmount, serviceTotal);
                serviceOrder.UpdatedAt = VietnamTime.Now;
            }
        }

        if (payment.RetailOrderId.HasValue)
        {
            var retailOrder = await _db.RetailOrders.FirstOrDefaultAsync(x => x.Id == payment.RetailOrderId.Value);
            if (retailOrder is not null)
            {
                var paidAmount = await _db.Payments
                    .Where(x => x.RetailOrderId == payment.RetailOrderId.Value && x.Status == PaymentStatusCompleted)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                // Retail order status follows existing DB check constraint.
                retailOrder.Status = paidAmount >= retailOrder.FinalAmount ? RetailOrderStatusConfirmed : RetailOrderStatusDraft;
                retailOrder.UpdatedAt = VietnamTime.Now;
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task TrySendPaymentSuccessEmailAsync(Payment payment)
    {
        try
        {
            _logger.LogInformation(
                "[PaymentConfirmationService] BÃ¡ÂºÂ¯t Ã„â€˜Ã¡ÂºÂ§u resolve email cho Payment #{PaymentId} " +
                "(RetailOrderId={RetailOrderId}, ServiceOrderId={ServiceOrderId}, PayerType={PayerType}, PayerId={PayerId})",
                payment.Id, payment.RetailOrderId, payment.ServiceOrderId, payment.PayerType, payment.PayerId);

            var recipient = await ResolveCustomerRecipientAsync(payment);
            if (!recipient.HasValue || string.IsNullOrWhiteSpace(recipient.Value.Email))
            {
                _logger.LogWarning(
                    "[PaymentConfirmationService] Ã¢ÂÅ’ KhÃƒÂ´ng tÃƒÂ¬m Ã„â€˜Ã†Â°Ã¡Â»Â£c email khÃƒÂ¡ch hÃƒÂ ng cho payment #{PaymentId} " +
                    "(RetailOrderId={RetailOrderId}, ServiceOrderId={ServiceOrderId}, PayerType={PayerType}, PayerId={PayerId}). " +
                    "Email bÃ¡Â»â€¹ bÃ¡Â»Â qua. HÃƒÂ£y kiÃ¡Â»Æ’m tra: (1) Customer cÃƒÂ³ email khÃƒÂ´ng? (2) BuyerEmail trong Order? (3) Invoice.ReceiveEmail?",
                    payment.Id, payment.RetailOrderId, payment.ServiceOrderId, payment.PayerType, payment.PayerId);
                return;
            }

            _logger.LogInformation(
                "[PaymentConfirmationService] Ã¢Å“â€¦ Ã„ÂÃƒÂ£ tÃƒÂ¬m Ã„â€˜Ã†Â°Ã¡Â»Â£c email: {Email} ({FullName}) cho Payment #{PaymentId}",
                recipient.Value.Email, recipient.Value.FullName, payment.Id);

            var amount = payment.Amount.ToString("N0");
            var body = $@"
                <h3>MyGarage - Thanh toÃ¡n thÃ nh cÃ´ng</h3>
                <p>Xin chÃ o {recipient.Value.FullName},</p>
                <p>ChÃºng tÃ´i dÃ£ nh?n thanh toÃ¡n thÃ nh cÃ´ng cho giao d?ch <b>{payment.TransactionCode}</b>.</p>
                <ul>
                  <li>S? ti?n: <b>{amount} VND</b></li>
                  <li>Tr?ng thÃ¡i: <b>{payment.Status}</b></li>
                  <li>Th?i gian: <b>{VietnamTime.Now:dd/MM/yyyy HH:mm:ss}</b></li>
                </ul>
                <p>C?m on b?n dÃ£ s? d?ng d?ch v? MyGarage.</p>";

            await _emailService.SendEmailAsync(recipient.Value.Email, "MyGarage - Thanh toÃ¡n thÃ nh cÃ´ng", body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PaymentConfirmationService] GÃ¡Â»Â­i email thÃ¡ÂºÂ¥t bÃ¡ÂºÂ¡i cho payment #{PaymentId}", payment.Id);
        }
    }

    private static string ResolveDocumentPaymentStatus(decimal paidAmount, decimal totalAmount)
    {
        if (totalAmount <= 0m)
        {
            return DocumentPaymentStatusPaid;
        }

        if (paidAmount <= 0m)
        {
            return DocumentPaymentStatusUnpaid;
        }

        if (paidAmount < totalAmount)
        {
            return DocumentPaymentStatusPartial;
        }

        return DocumentPaymentStatusPaid;
    }

    private async Task<(string Email, string FullName)?> ResolveCustomerRecipientAsync(Payment payment)
    {
        // BÃ†Â°Ã¡Â»â€ºc 1: TÃƒÂ¬m qua PayerType/PayerId trÃ¡Â»Â±c tiÃ¡ÂºÂ¿p trÃƒÂªn Payment
        if (string.Equals(payment.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) && payment.PayerId.HasValue)
        {
            _logger.LogDebug("[ResolveEmail] BÃ†Â°Ã¡Â»â€ºc 1: TÃƒÂ¬m Customer theo PayerId={PayerId}", payment.PayerId.Value);
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == payment.PayerId.Value);
            if (customer is not null && !string.IsNullOrWhiteSpace(customer.Email))
            {
                return (customer.Email!, customer.FullName ?? "KhÃ¡ch hÃ ng");
            }
            _logger.LogDebug("[ResolveEmail] BÃ†Â°Ã¡Â»â€ºc 1: Customer #{PayerId} khÃƒÂ´ng cÃƒÂ³ email hoÃ¡ÂºÂ·c khÃƒÂ´ng tÃ¡Â»â€œn tÃ¡ÂºÂ¡i", payment.PayerId.Value);
        }

        // BÃ†Â°Ã¡Â»â€ºc 2: TÃƒÂ¬m qua ServiceOrder Ã¢â€ â€™ Customer
        if (payment.ServiceOrderId.HasValue)
        {
            _logger.LogDebug("[ResolveEmail] BÃ†Â°Ã¡Â»â€ºc 2: TÃƒÂ¬m qua ServiceOrderId={ServiceOrderId}", payment.ServiceOrderId.Value);
            var serviceOrder = await _db.ServiceOrders
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.Id == payment.ServiceOrderId.Value);
            if (serviceOrder?.Customer is not null && !string.IsNullOrWhiteSpace(serviceOrder.Customer.Email))
            {
                return (serviceOrder.Customer.Email!, serviceOrder.Customer.FullName ?? "KhÃ¡ch hÃ ng");
            }
            _logger.LogDebug("[ResolveEmail] BÃ†Â°Ã¡Â»â€ºc 2: ServiceOrder khÃƒÂ´ng cÃƒÂ³ Customer.Email");
        }

        // BÃ†Â°Ã¡Â»â€ºc 3: TÃƒÂ¬m qua RetailOrder Ã¢â€ â€™ Customer hoÃ¡ÂºÂ·c Invoice.ReceiveEmail
        if (payment.RetailOrderId.HasValue)
        {
            _logger.LogDebug("[ResolveEmail] BÃ†Â°Ã¡Â»â€ºc 3: TÃƒÂ¬m qua RetailOrderId={RetailOrderId}", payment.RetailOrderId.Value);
            var retailOrder = await _db.RetailOrders
                .Include(r => r.Customer)
                .Include(r => r.Invoices)
                .FirstOrDefaultAsync(r => r.Id == payment.RetailOrderId.Value);
            if (retailOrder?.Customer is not null && !string.IsNullOrWhiteSpace(retailOrder.Customer.Email))
            {
                return (retailOrder.Customer.Email!, retailOrder.Customer.FullName ?? "KhÃ¡ch hÃ ng");
            }
            _logger.LogDebug("[ResolveEmail] BÃ†Â°Ã¡Â»â€ºc 3: RetailOrder.Customer khÃƒÂ´ng cÃƒÂ³ email (CustomerId={CustomerId})",
                retailOrder?.CustomerId);

            var invoiceReceiveEmail = retailOrder?.Invoices
                .OrderByDescending(i => i.IssuedAt ?? i.CreatedAt ?? DateTime.MinValue)
                .Select(i => i.ReceiveEmail)
                .FirstOrDefault(email => !string.IsNullOrWhiteSpace(email));

            if (!string.IsNullOrWhiteSpace(invoiceReceiveEmail))
            {
                _logger.LogDebug("[ResolveEmail] BÃ†Â°Ã¡Â»â€ºc 3b: Ã¢Å“â€¦ TÃƒÂ¬m thÃ¡ÂºÂ¥y ReceiveEmail={Email} tÃ¡Â»Â« Invoice", invoiceReceiveEmail);
                return (invoiceReceiveEmail!, retailOrder?.Customer?.FullName ?? "KhÃƒÂ¡ch hÃƒÂ ng");
            }
        }

        // BÃ†Â°Ã¡Â»â€ºc 4: TÃƒÂ¬m qua in-memory OrderStore (BuyerEmail tÃ¡Â»Â« PayOS order)
        if (!string.IsNullOrWhiteSpace(payment.TransactionCode))
        {
            _logger.LogDebug("[ResolveEmail] BÃ†Â°Ã¡Â»â€ºc 4: TÃƒÂ¬m qua OrderStore, TransactionCode={TransactionCode}", payment.TransactionCode);
            var order = _orderStore.GetOrderByPaymentLinkId(payment.TransactionCode);
            if (order is not null && !string.IsNullOrWhiteSpace(order.BuyerEmail))
            {
                _logger.LogDebug("[ResolveEmail] BÃ†Â°Ã¡Â»â€ºc 4: Ã¢Å“â€¦ TÃƒÂ¬m thÃ¡ÂºÂ¥y BuyerEmail={Email} tÃ¡Â»Â« OrderStore", order.BuyerEmail);
                return (order.BuyerEmail!, string.IsNullOrWhiteSpace(order.BuyerName) ? "KhÃƒÂ¡ch hÃƒÂ ng" : order.BuyerName);
            }
            _logger.LogDebug("[ResolveEmail] BÃ†Â°Ã¡Â»â€ºc 4: OrderStore order khÃƒÂ´ng cÃƒÂ³ BuyerEmail (order {Found})",
                order is not null ? "found" : "NOT found");
        }

        // BÃ†Â°Ã¡Â»â€ºc 5: TÃƒÂ¬m qua Invoice trÃ¡Â»Â±c tiÃ¡ÂºÂ¿p
        if (payment.InvoiceId.HasValue)
        {
            _logger.LogDebug("[ResolveEmail] BÃ†Â°Ã¡Â»â€ºc 5: TÃƒÂ¬m qua InvoiceId={InvoiceId}", payment.InvoiceId.Value);
            var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == payment.InvoiceId.Value);
            if (invoice is not null &&
                string.Equals(invoice.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) &&
                invoice.PayerId.HasValue)
            {
                var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == invoice.PayerId.Value);
                if (customer is not null && !string.IsNullOrWhiteSpace(customer.Email))
                {
                    return (customer.Email!, customer.FullName ?? "KhÃ¡ch hÃ ng");
                }
            }

            if (invoice is not null && !string.IsNullOrWhiteSpace(invoice.ReceiveEmail))
            {
                return (invoice.ReceiveEmail!, "KhÃ¡ch hÃ ng");
            }
        }

        // BÃ†Â°Ã¡Â»â€ºc 6 (MÃ¡Â»Å¡I): NÃ¡ÂºÂ¿u cÃƒÂ³ RetailOrderId, thÃ¡Â»Â­ tÃƒÂ¬m Customer trÃ¡Â»Â±c tiÃ¡ÂºÂ¿p qua RetailOrder.CustomerId
        // (fallback khi Customer.Email null nhÃ†Â°ng vÃ¡ÂºÂ«n cÃƒÂ³ thÃ¡Â»Æ’ tÃƒÂ¬m User account)
        if (payment.RetailOrderId.HasValue)
        {
            var retailOrderForFallback = await _db.RetailOrders
                .AsNoTracking()
                .Where(r => r.Id == payment.RetailOrderId.Value && r.CustomerId.HasValue)
                .Select(r => new { r.CustomerId })
                .FirstOrDefaultAsync();

            if (retailOrderForFallback?.CustomerId.HasValue == true)
            {
                var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == retailOrderForFallback.CustomerId.Value);
                if (customer is not null && !string.IsNullOrWhiteSpace(customer.Email))
                {
                    _logger.LogDebug("[ResolveEmail] BÃ†Â°Ã¡Â»â€ºc 6: Ã¢Å“â€¦ Fallback tÃƒÂ¬m thÃ¡ÂºÂ¥y email={Email} tÃ¡Â»Â« CustomerId={CustomerId}",
                        customer.Email, customer.Id);
                    return (customer.Email!, customer.FullName ?? "KhÃƒÂ¡ch hÃƒÂ ng");
                }
            }
        }

        _logger.LogWarning("[ResolveEmail] Ã¢ÂÅ’ KhÃƒÂ´ng tÃƒÂ¬m Ã„â€˜Ã†Â°Ã¡Â»Â£c email qua tÃ¡ÂºÂ¥t cÃ¡ÂºÂ£ 6 bÃ†Â°Ã¡Â»â€ºc cho Payment #{PaymentId}", payment.Id);
        return null;
    }

    private async Task<(string Email, string FullName)?> ResolveRetailOrderRecipientAsync(long retailOrderId)
    {
        var retailOrder = await _db.RetailOrders
            .Include(r => r.Customer)
            .Include(r => r.Invoices)
            .FirstOrDefaultAsync(r => r.Id == retailOrderId);

        if (retailOrder is null)
        {
            return null;
        }

        if (retailOrder.Customer is not null && !string.IsNullOrWhiteSpace(retailOrder.Customer.Email))
        {
            return (retailOrder.Customer.Email!, retailOrder.Customer.FullName ?? "KhÃƒÂ¡ch hÃƒÂ ng");
        }

        var invoiceReceiveEmail = retailOrder.Invoices
            .OrderByDescending(i => i.IssuedAt ?? i.CreatedAt ?? DateTime.MinValue)
            .Select(i => i.ReceiveEmail)
            .FirstOrDefault(email => !string.IsNullOrWhiteSpace(email));

        if (!string.IsNullOrWhiteSpace(invoiceReceiveEmail))
        {
            return (invoiceReceiveEmail!, retailOrder.Customer?.FullName ?? "KhÃƒÂ¡ch hÃƒÂ ng");
        }

        return null;
    }

    public async Task SendRetailOrderSuccessEmailAsync(
        long retailOrderId,
        decimal amountPaid,
        string transactionCode,
        string status,
        System.DateTimeOffset transactionTime,
        string? fallbackEmail = null,
        string? fallbackFullName = null)
    {
        try
        {
            var retailOrder = await _db.RetailOrders
                .Include(r => r.RetailOrderItems)
                .ThenInclude(ri => ri.Product)
                .FirstOrDefaultAsync(r => r.Id == retailOrderId);

            if (retailOrder == null)
            {
                _logger.LogWarning("[PaymentConfirmationService] KhÃƒÂ´ng tÃƒÂ¬m thÃ¡ÂºÂ¥y retail order #{RetailOrderId} Ã„â€˜Ã¡Â»Æ’ gÃ¡Â»Â­i email hÃƒÂ³a Ã„â€˜Ã†Â¡n.", retailOrderId);
                return;
            }

            var recipient = await ResolveRetailOrderRecipientAsync(retailOrderId);
            if ((!recipient.HasValue || string.IsNullOrWhiteSpace(recipient.Value.Email)) && !string.IsNullOrWhiteSpace(fallbackEmail))
            {
                recipient = (fallbackEmail!, string.IsNullOrWhiteSpace(fallbackFullName) ? "KhÃƒÂ¡ch hÃƒÂ ng" : fallbackFullName.Trim());
            }

            if (!recipient.HasValue || string.IsNullOrWhiteSpace(recipient.Value.Email))
            {
                _logger.LogWarning(
                    "[PaymentConfirmationService] KhÃƒÂ´ng tÃƒÂ¬m Ã„â€˜Ã†Â°Ã¡Â»Â£c email ngÃ†Â°Ã¡Â»Âi nhÃ¡ÂºÂ­n cho retail order #{RetailOrderId}. " +
                    "Email hÃƒÂ³a Ã„â€˜Ã†Â¡n bÃ¡Â»â€¹ bÃ¡Â»Â qua.",
                    retailOrderId);
                return;
            }

            var amount = amountPaid.ToString("N0");
            var body = $@"
<div style=""font-family: Arial, sans-serif; background-color: #f4f7f6; padding: 20px; color: #333;"">
    <div style=""max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);"">
        <!-- Header -->
        <div style=""background-color: #0056b3; color: #ffffff; padding: 20px; text-align: center;"">
            <h2 style=""margin: 0; font-size: 24px;"">MyGarage</h2>
            <p style=""margin: 5px 0 0; font-size: 16px; opacity: 0.9;"">HÃƒÂ³a Ã„ÂÃ†Â¡n / XÃƒÂ¡c nhÃ¡ÂºÂ­n thanh toÃƒÂ¡n</p>
        </div>

        <!-- Content -->
        <div style=""padding: 20px; line-height: 1.6;"">
            <p>Xin chÃƒÂ o <strong>{recipient.Value.FullName}</strong>,</p>
            <p>TuyÃ¡Â»â€¡t vÃ¡Â»Âi! ChÃƒÂºng tÃƒÂ´i Ã„â€˜ÃƒÂ£ xÃƒÂ¡c nhÃ¡ÂºÂ­n khoÃ¡ÂºÂ£n thanh toÃƒÂ¡n thÃƒÂ nh cÃƒÂ´ng cho giao dÃ¡Â»â€¹ch <b>{transactionCode}</b> cÃ¡Â»Â§a bÃ¡ÂºÂ¡n.</p>

            <div style=""background-color: #f8f9fa; border-left: 4px solid #28a745; padding: 10px 15px; margin: 20px 0;"">
                <p style=""margin: 0;""><strong>SÃ¡Â»â€˜ tiÃ¡Â»Ân:</strong> <span style=""color: #d9534f; font-weight: bold; font-size: 16px;"">{amount} VND</span></p>
                <p style=""margin: 5px 0 0;""><strong>TrÃ¡ÂºÂ¡ng thÃƒÂ¡i:</strong> <span style=""color: #28a745;"">ThÃƒÂ nh cÃƒÂ´ng ({status})</span></p>
                <p style=""margin: 5px 0 0;""><strong>ThÃ¡Â»Âi gian:</strong> {transactionTime:dd/MM/yyyy HH:mm:ss}</p>
            </div>";

            if (retailOrder.RetailOrderItems.Any())
            {
                body += $@"
            <h4 style=""border-bottom: 2px solid #eeeeee; padding-bottom: 8px; margin-top: 30px; color: #0056b3;"">
                Chi tiÃ¡ÂºÂ¿t mua hÃƒÂ ng (MÃƒÂ£ Ã„â€˜Ã†Â¡n: {retailOrder.OrderCode})
            </h4>
            <table style=""width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 14px;"">
                <thead>
                    <tr style=""background-color: #f1f1f1; text-align: left;"">
                        <th style=""padding: 10px; border-bottom: 1px solid #ddd;"">SÃ¡ÂºÂ£n phÃ¡ÂºÂ©m</th>
                        <th style=""padding: 10px; border-bottom: 1px solid #ddd; text-align: center;"">SL</th>
                        <th style=""padding: 10px; border-bottom: 1px solid #ddd; text-align: right;"">Ã„ÂÃ†Â¡n giÃƒÂ¡</th>
                        <th style=""padding: 10px; border-bottom: 1px solid #ddd; text-align: right;"">ThÃƒÂ nh tiÃ¡Â»Ân</th>
                    </tr>
                </thead>
                <tbody>";

                foreach (var item in retailOrder.RetailOrderItems)
                {
                    var prodName = item.Product?.Name ?? "SÃ¡ÂºÂ£n phÃ¡ÂºÂ©m";
                    body += $@"
                    <tr>
                        <td style=""padding: 10px; border-bottom: 1px solid #eee;"">{prodName}</td>
                        <td style=""padding: 10px; border-bottom: 1px solid #eee; text-align: center;"">{item.Quantity:N0}</td>
                        <td style=""padding: 10px; border-bottom: 1px solid #eee; text-align: right;"">{item.UnitPriceAtTime:N0}</td>
                        <td style=""padding: 10px; border-bottom: 1px solid #eee; text-align: right; font-weight: bold;"">{item.FinalAmount:N0}</td>
                    </tr>";
                }

                body += $@"
                </tbody>
                <tfoot>
                    <tr>
                        <td colspan=""3"" style=""padding: 10px 10px 5px; text-align: right; color: #555;"">TÃ¡ÂºÂ¡m tÃƒÂ­nh:</td>
                        <td style=""padding: 10px 10px 5px; text-align: right;"">{retailOrder.SubTotal:N0} Ã¢â€šÂ«</td>
                    </tr>
                    <tr>
                        <td colspan=""3"" style=""padding: 5px 10px; text-align: right; color: #555;"">GiÃ¡ÂºÂ£m giÃƒÂ¡:</td>
                        <td style=""padding: 5px 10px; text-align: right; color: #28a745;"">-{(retailOrder.TotalDiscountAmount ?? 0):N0} Ã¢â€šÂ«</td>
                    </tr>
                    <tr>
                        <td colspan=""3"" style=""padding: 5px 10px; text-align: right; color: #555;"">ThuÃ¡ÂºÂ¿:</td>
                        <td style=""padding: 5px 10px; text-align: right;"">+{(retailOrder.TaxAmount ?? 0):N0} Ã¢â€šÂ«</td>
                    </tr>
                    <tr>
                        <td colspan=""3"" style=""padding: 15px 10px; text-align: right; font-weight: bold; font-size: 16px; border-top: 2px solid #ddd;"">TÃ¡Â»â€NG CÃ¡Â»ËœNG:</td>
                        <td style=""padding: 15px 10px; text-align: right; font-weight: bold; font-size: 16px; color: #d9534f; border-top: 2px solid #ddd;"">{retailOrder.FinalAmount:N0} Ã¢â€šÂ«</td>
                    </tr>
                </tfoot>
            </table>";
            }

            body += $@"
            <p style=""margin-top: 30px;"">NÃ¡ÂºÂ¿u bÃ¡ÂºÂ¡n cÃƒÂ³ bÃ¡ÂºÂ¥t kÃ¡Â»Â³ cÃƒÂ¢u hÃ¡Â»Âi nÃƒÂ o, vui lÃƒÂ²ng liÃƒÂªn hÃ¡Â»â€¡ vÃ¡Â»â€ºi chÃƒÂºng tÃƒÂ´i qua sÃ¡Â»â€˜ Ã„â€˜iÃ¡Â»â€¡n thoÃ¡ÂºÂ¡i hÃ¡Â»â€” trÃ¡Â»Â£ hoÃ¡ÂºÂ·c phÃ¡ÂºÂ£n hÃ¡Â»â€œi trÃ¡Â»Â±c tiÃ¡ÂºÂ¿p qua email nÃƒÂ y.</p>
            <p>TrÃƒÂ¢n trÃ¡Â»Âng,<br><strong>Ã„ÂÃ¡Â»â„¢i ngÃ…Â© MyGarage</strong></p>
        </div>

        <!-- Footer -->
        <div style=""background-color: #f1f5f9; padding: 15px; text-align: center; font-size: 12px; color: #888;"">
            <p style=""margin: 0;"">Ã‚Â© {System.DateTime.UtcNow.Year} MyGarage. All rights reserved.</p>
            <p style=""margin: 5px 0 0;"">Email nÃƒÂ y Ã„â€˜Ã†Â°Ã¡Â»Â£c gÃ¡Â»Â­i tÃ¡Â»Â± Ã„â€˜Ã¡Â»â„¢ng tÃ¡Â»Â« hÃ¡Â»â€¡ thÃ¡Â»â€˜ng. Vui lÃƒÂ²ng khÃƒÂ´ng trÃ¡ÂºÂ£ lÃ¡Â»Âi bÃ¡ÂºÂ±ng email rÃƒÂ¡c.</p>
        </div>
    </div>
</div>";

            await _emailService.SendEmailAsync(recipient.Value.Email, "MyGarage - Thanh toÃƒÂ¡n thÃƒÂ nh cÃƒÂ´ng & HÃƒÂ³a Ã„â€˜Ã†Â¡n mua hÃƒÂ ng", body);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "[PaymentConfirmationService] GÃ¡Â»Â­i retail email thÃ¡ÂºÂ¥t bÃ¡ÂºÂ¡i cho RetailOrderId #{RetailOrderId}", retailOrderId);
        }
    }
}

