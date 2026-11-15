using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;
using System.Net;
using System.Text;
using MyGarage.Domain.Payments;

namespace MyGarage.Services;

public class PaymentConfirmationService : IPaymentConfirmationService
{
    private const string PaymentStatusCompleted = nameof(PaymentStatusCode.COMPLETED);
    private const string PaymentStatusCanceled = nameof(PaymentStatusCode.CANCELED);
    private const string PaymentStatusFailed = nameof(PaymentStatusCode.FAILED);
    private const string PaymentStatusPending = nameof(PaymentStatusCode.PENDING);
    private const string RetailOrderStatusConfirmed = nameof(RetailOrderStatus.CONFIRMED);
    private const string RetailOrderStatusDraft = nameof(RetailOrderStatus.DRAFT);
    private readonly IGarageContext _db;
    private readonly IInvoiceService _invoiceService;
    private readonly IEmailService _emailService;
    private readonly IOrderStore _orderStore;
    private readonly ILogger<PaymentConfirmationService> _logger;

    public PaymentConfirmationService(
        IGarageContext db,
        IInvoiceService invoiceService,
        IEmailService emailService,
        IOrderStore orderStore,
        ILogger<PaymentConfirmationService> logger)
    {
        _db = db;
        _invoiceService = invoiceService;
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
        await SaveChangesInTransactionAsync();

        await UpdateRelatedDocumentsAsync(payment);

        if (previousStatus != PaymentStatusCompleted && normalizedStatus == PaymentStatusCompleted)
        {
            if (previousStatus != nameof(PaymentStatusCode.COMPLETED))
            {
                // Transition mới từ non-COMPLETED -> COMPLETED -> luôn gửi
                _logger.LogInformation(
                    "[PaymentConfirmationService] Transition {PreviousStatus} -> COMPLETED cho Payment #{PaymentId}. Gửi email.",
                    previousStatus, payment.Id);
                await TrySendPaymentSuccessEmailAsync(payment);
            }
            else
            {
                // previousStatus đã là COMPLETED (webhook chạy trước hoặc retry).
                // Vẫn gửi email vì webhook chỉ set status, không gửi email.
                _logger.LogInformation(
                    "[PaymentConfirmationService] Payment #{PaymentId} đã COMPLETED (có thể do webhook). " +
                    "Retry gửi email để đảm bảo khách nhận được hóa đơn.",
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
        await EnsureInvoiceLinkedAsync(payment);

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
                invoice.PaymentStatus = DocumentPaymentStatusRules.Resolve(paidAmount, invoiceTotal);
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
                serviceOrder.PaymentStatus = DocumentPaymentStatusRules.Resolve(paidAmount, serviceTotal);
                serviceOrder.UpdatedAt = VietnamTime.Now;
            }
        }

        long? confirmedRetailOrderId = null;
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
                if (string.Equals(retailOrder.Status, RetailOrderStatusConfirmed, StringComparison.OrdinalIgnoreCase))
                {
                    confirmedRetailOrderId = retailOrder.Id;
                }
            }
        }

        await SaveChangesInTransactionAsync();

        if (confirmedRetailOrderId.HasValue)
        {
            await RetailOrderStockCommitter.EnsureStockDeductedAsync(_db, confirmedRetailOrderId.Value);
        }
    }

    public async Task EnsureInvoiceLinkedAsync(Payment payment)
    {
        if (payment == null)
        {
            return;
        }

        var normalizedStatus = NormalizeStatus(payment.Status);
        if (normalizedStatus != PaymentStatusCompleted)
        {
            return;
        }

        var invoice = await _invoiceService.EnsureForPaymentContextAsync(
            payment.ServiceOrderId,
            payment.RetailOrderId,
            payment.PayerType,
            payment.PayerId);

        if (invoice == null)
        {
            return;
        }

        var changed = false;
        if (!payment.InvoiceId.HasValue || payment.InvoiceId.Value != invoice.Id)
        {
            payment.InvoiceId = invoice.Id;
            changed = true;
        }

        if (!string.Equals(payment.Direction, "INFLOW", StringComparison.OrdinalIgnoreCase))
        {
            payment.Direction = "INFLOW";
            changed = true;
        }

        if (changed)
        {
            payment.UpdatedAt = VietnamTime.Now;
            await SaveChangesInTransactionAsync();
        }
    }

    private async Task TrySendPaymentSuccessEmailAsync(Payment payment)
    {
        try
        {
            _logger.LogInformation(
                "[PaymentConfirmationService] Bắt đầu resolve email cho Payment #{PaymentId} " +
                "(RetailOrderId={RetailOrderId}, ServiceOrderId={ServiceOrderId}, PayerType={PayerType}, PayerId={PayerId})",
                payment.Id, payment.RetailOrderId, payment.ServiceOrderId, payment.PayerType, payment.PayerId);

            var recipient = await ResolveCustomerRecipientAsync(payment);
            if (!recipient.HasValue || string.IsNullOrWhiteSpace(recipient.Value.Email))
            {
                _logger.LogWarning(
                    "[PaymentConfirmationService] Không tìm được email khách hàng cho payment #{PaymentId} " +
                    "(RetailOrderId={RetailOrderId}, ServiceOrderId={ServiceOrderId}, PayerType={PayerType}, PayerId={PayerId}). " +
                    "Email bị bỏ qua. Hãy kiểm tra: (1) Customer có email không? (2) BuyerEmail trong Order? (3) Invoice.ReceiveEmail?",
                    payment.Id, payment.RetailOrderId, payment.ServiceOrderId, payment.PayerType, payment.PayerId);
                return;
            }

            _logger.LogInformation(
                "[PaymentConfirmationService] Đã tìm được email: {Email} ({FullName}) cho Payment #{PaymentId}",
                recipient.Value.Email, recipient.Value.FullName, payment.Id);

            var body = BuildPaymentSuccessEmailBody(
                recipient.Value.FullName,
                payment.TransactionCode,
                payment.Amount,
                payment.Status,
                VietnamTime.Now,
                null);

            await _emailService.SendEmailAsync(recipient.Value.Email, "MyGarage - Thanh toán thành công", body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PaymentConfirmationService] Gửi email thất bại cho payment #{PaymentId}", payment.Id);
        }
    }

    private async Task<(string Email, string FullName)?> ResolveCustomerRecipientAsync(Payment payment)
    {
        // Bước 1: Tìm qua PayerType/PayerId trực tiếp trên Payment
        if (string.Equals(payment.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) && payment.PayerId.HasValue)
        {
            _logger.LogDebug("[ResolveEmail] Bước 1: Tìm Customer theo PayerId={PayerId}", payment.PayerId.Value);
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == payment.PayerId.Value);
            if (customer is not null && !string.IsNullOrWhiteSpace(customer.Email))
            {
                return (customer.Email!, customer.FullName ?? "Khách hàng");
            }
            _logger.LogDebug("[ResolveEmail] Bước 1: Customer #{PayerId} không có email hoặc không tồn tại", payment.PayerId.Value);
        }

        // Bước 2: Tìm qua ServiceOrder -> Customer
        if (payment.ServiceOrderId.HasValue)
        {
            _logger.LogDebug("[ResolveEmail] Bước 2: Tìm qua ServiceOrderId={ServiceOrderId}", payment.ServiceOrderId.Value);
            var serviceOrder = await _db.ServiceOrders
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.Id == payment.ServiceOrderId.Value);
            if (serviceOrder?.Customer is not null && !string.IsNullOrWhiteSpace(serviceOrder.Customer.Email))
            {
                return (serviceOrder.Customer.Email!, serviceOrder.Customer.FullName ?? "Khách hàng");
            }
            _logger.LogDebug("[ResolveEmail] Bước 2: ServiceOrder không có Customer.Email");
        }

        // Bước 3: Tìm qua RetailOrder -> Customer hoặc Invoice.ReceiveEmail
        if (payment.RetailOrderId.HasValue)
        {
            _logger.LogDebug("[ResolveEmail] Bước 3: Tìm qua RetailOrderId={RetailOrderId}", payment.RetailOrderId.Value);
            var retailOrder = await _db.RetailOrders
                .Include(r => r.Customer)
                .Include(r => r.Invoices)
                .FirstOrDefaultAsync(r => r.Id == payment.RetailOrderId.Value);
            if (retailOrder?.Customer is not null && !string.IsNullOrWhiteSpace(retailOrder.Customer.Email))
            {
                return (retailOrder.Customer.Email!, retailOrder.Customer.FullName ?? "Khách hàng");
            }
            _logger.LogDebug("[ResolveEmail] Bước 3: RetailOrder.Customer không có email (CustomerId={CustomerId})",
                retailOrder?.CustomerId);

            var invoiceReceiveEmail = retailOrder?.Invoices
                .OrderByDescending(i => i.IssuedAt ?? i.CreatedAt ?? DateTime.MinValue)
                .Select(i => i.ReceiveEmail)
                .FirstOrDefault(email => !string.IsNullOrWhiteSpace(email));

            if (!string.IsNullOrWhiteSpace(invoiceReceiveEmail))
            {
                _logger.LogDebug("[ResolveEmail] Bước 3b: Tìm thấy ReceiveEmail={Email} từ Invoice", invoiceReceiveEmail);
                return (invoiceReceiveEmail!, retailOrder?.Customer?.FullName ?? "Khách hàng");
            }
        }

        // Bước 4: Tìm qua in-memory OrderStore (BuyerEmail từ PayOS order)
        if (!string.IsNullOrWhiteSpace(payment.TransactionCode))
        {
            _logger.LogDebug("[ResolveEmail] Bước 4: Tìm qua OrderStore, TransactionCode={TransactionCode}", payment.TransactionCode);
            var order = _orderStore.GetOrderByPaymentLinkId(payment.TransactionCode);
            if (order is not null && !string.IsNullOrWhiteSpace(order.BuyerEmail))
            {
                _logger.LogDebug("[ResolveEmail] Bước 4: Tìm thấy BuyerEmail={Email} từ OrderStore", order.BuyerEmail);
                return (order.BuyerEmail!, string.IsNullOrWhiteSpace(order.BuyerName) ? "Khách hàng" : order.BuyerName);
            }
            _logger.LogDebug("[ResolveEmail] Bước 4: OrderStore order không có BuyerEmail (order {Found})",
                order is not null ? "found" : "NOT found");
        }

        // Bước 5: Tìm qua Invoice trực tiếp
        if (payment.InvoiceId.HasValue)
        {
            _logger.LogDebug("[ResolveEmail] Bước 5: Tìm qua InvoiceId={InvoiceId}", payment.InvoiceId.Value);
            var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == payment.InvoiceId.Value);
            if (invoice is not null &&
                string.Equals(invoice.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) &&
                invoice.PayerId.HasValue)
            {
                var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == invoice.PayerId.Value);
                if (customer is not null && !string.IsNullOrWhiteSpace(customer.Email))
                {
                    return (customer.Email!, customer.FullName ?? "Khách hàng");
                }
            }

            if (invoice is not null && !string.IsNullOrWhiteSpace(invoice.ReceiveEmail))
            {
                return (invoice.ReceiveEmail!, "Khách hàng");
            }
        }
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
                    _logger.LogDebug("[ResolveEmail] Bước 6: Fallback tìm thấy email={Email} từ CustomerId={CustomerId}",
                        customer.Email, customer.Id);
                    return (customer.Email!, customer.FullName ?? "Khách hàng");
                }
            }
        }

        _logger.LogWarning("[ResolveEmail] Không tìm được email qua tất cả 6 bước cho Payment #{PaymentId}", payment.Id);
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
            return (retailOrder.Customer.Email!, retailOrder.Customer.FullName ?? "Khách hàng");
        }

        var invoiceReceiveEmail = retailOrder.Invoices
            .OrderByDescending(i => i.IssuedAt ?? i.CreatedAt ?? DateTime.MinValue)
            .Select(i => i.ReceiveEmail)
            .FirstOrDefault(email => !string.IsNullOrWhiteSpace(email));

        if (!string.IsNullOrWhiteSpace(invoiceReceiveEmail))
        {
            return (invoiceReceiveEmail!, retailOrder.Customer?.FullName ?? "Khách hàng");
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
                _logger.LogWarning("[PaymentConfirmationService] Không tìm thấy retail order #{RetailOrderId} để gửi email hóa đơn.", retailOrderId);
                return;
            }

            var recipient = await ResolveRetailOrderRecipientAsync(retailOrderId);
            if ((!recipient.HasValue || string.IsNullOrWhiteSpace(recipient.Value.Email)) && !string.IsNullOrWhiteSpace(fallbackEmail))
            {
                recipient = (fallbackEmail!, string.IsNullOrWhiteSpace(fallbackFullName) ? "Khách hàng" : fallbackFullName.Trim());
            }

            if (!recipient.HasValue || string.IsNullOrWhiteSpace(recipient.Value.Email))
            {
                _logger.LogWarning(
                    "[PaymentConfirmationService] Không tìm được email người nhận cho retail order #{RetailOrderId}. " +
                    "Email hóa đơn bị bỏ qua.",
                    retailOrderId);
                return;
            }

            var body = BuildPaymentSuccessEmailBody(
                recipient.Value.FullName,
                transactionCode,
                amountPaid,
                status,
                transactionTime.LocalDateTime,
                BuildRetailOrderSummaryHtml(retailOrder));

            await _emailService.SendEmailAsync(recipient.Value.Email, "MyGarage - Thanh toán thành công & Hóa đơn mua hàng", body);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "[PaymentConfirmationService] Gửi retail email thất bại cho RetailOrderId #{RetailOrderId}", retailOrderId);
        }
    }

    private static string BuildPaymentSuccessEmailBody(
        string? fullName,
        string? transactionCode,
        decimal amountPaid,
        string? status,
        DateTime transactionTime,
        string? detailSectionHtml)
    {
        var customerName = Html(fullName, "Khách hàng");
        var code = Html(transactionCode, "N/A");
        var normalizedStatus = Html((status ?? PaymentStatusCompleted).Trim().ToUpperInvariant(), PaymentStatusCompleted);
        var amount = FormatCurrency(amountPaid);

        var content = $@"
            <p style=""margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155;"">Xin chào <strong>{customerName}</strong>,</p>
            <p style=""margin:0 0 20px;font-size:16px;line-height:1.7;color:#334155;"">
                MyGarage đã ghi nhận thanh toán thành công cho giao dịch <strong>{code}</strong>.
            </p>

            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""margin:0 0 24px;border-collapse:separate;border-spacing:0;background:#f8fafc;border:1px solid #e2e8f0;border-radius:14px;"">
                <tr>
                    <td style=""padding:18px 20px;border-bottom:1px solid #e2e8f0;"">
                        <div style=""font-size:12px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:#64748b;margin-bottom:6px;"">Số tiền</div>
                        <div style=""font-size:28px;font-weight:800;line-height:1.2;color:#0f172a;"">{amount}</div>
                    </td>
                </tr>
                <tr>
                    <td style=""padding:0 20px;"">
                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""border-collapse:collapse;"">
                            <tr>
                                <td style=""padding:14px 0;border-bottom:1px solid #e2e8f0;font-size:14px;color:#64748b;width:34%;"">Trạng thái</td>
                                <td style=""padding:14px 0;border-bottom:1px solid #e2e8f0;font-size:14px;color:#0f172a;font-weight:700;text-align:right;"">{normalizedStatus}</td>
                            </tr>
                            <tr>
                                <td style=""padding:14px 0;font-size:14px;color:#64748b;"">Thời gian</td>
                                <td style=""padding:14px 0;font-size:14px;color:#0f172a;font-weight:700;text-align:right;"">{transactionTime:dd/MM/yyyy HH:mm:ss}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
            {detailSectionHtml ?? string.Empty}
            <p style=""margin:24px 0 0;font-size:15px;line-height:1.7;color:#475569;"">
                Nếu bạn cần hỗ trợ thêm, chỉ cần phản hồi email này hoặc liên hệ trực tiếp với MyGarage.
            </p>
            <p style=""margin:18px 0 0;font-size:15px;line-height:1.7;color:#475569;"">
                Trân trọng,<br><strong>Đội ngũ MyGarage</strong>
            </p>";

        return WrapEmailLayout("Thanh toán thành công", "Xác nhận giao dịch và hóa đơn", content);
    }

    private static string BuildRetailOrderSummaryHtml(RetailOrder retailOrder)
    {
        if (!retailOrder.RetailOrderItems.Any())
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append($@"
            <div style=""margin:0 0 24px;"">
                <div style=""font-size:18px;font-weight:800;color:#0f172a;margin:0 0 12px;"">Chi tiết đơn hàng</div>
                <div style=""font-size:14px;color:#64748b;margin:0 0 12px;"">Mã đơn: <strong style=""color:#0f172a;"">{Html(retailOrder.OrderCode, "N/A")}</strong></div>
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""border-collapse:collapse;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;"">
                    <thead>
                        <tr style=""background:#f8fafc;"">
                            <th align=""left"" style=""padding:12px 14px;font-size:12px;letter-spacing:.06em;text-transform:uppercase;color:#64748b;border-bottom:1px solid #e2e8f0;"">Sản phẩm</th>
                            <th align=""center"" style=""padding:12px 14px;font-size:12px;letter-spacing:.06em;text-transform:uppercase;color:#64748b;border-bottom:1px solid #e2e8f0;"">SL</th>
                            <th align=""right"" style=""padding:12px 14px;font-size:12px;letter-spacing:.06em;text-transform:uppercase;color:#64748b;border-bottom:1px solid #e2e8f0;"">Đơn giá</th>
                            <th align=""right"" style=""padding:12px 14px;font-size:12px;letter-spacing:.06em;text-transform:uppercase;color:#64748b;border-bottom:1px solid #e2e8f0;"">Thành tiền</th>
                        </tr>
                    </thead>
                    <tbody>");

        foreach (var item in retailOrder.RetailOrderItems)
        {
            builder.Append($@"
                        <tr>
                            <td style=""padding:14px;border-bottom:1px solid #e2e8f0;font-size:14px;color:#0f172a;"">{Html(item.Product?.Name, $"Sản phẩm #{item.ProductId}")}</td>
                            <td align=""center"" style=""padding:14px;border-bottom:1px solid #e2e8f0;font-size:14px;color:#334155;"">{item.Quantity:N0}</td>
                            <td align=""right"" style=""padding:14px;border-bottom:1px solid #e2e8f0;font-size:14px;color:#334155;"">{FormatCurrency(item.UnitPriceAtTime)}</td>
                            <td align=""right"" style=""padding:14px;border-bottom:1px solid #e2e8f0;font-size:14px;color:#0f172a;font-weight:700;"">{FormatCurrency(item.FinalAmount)}</td>
                        </tr>");
        }

        builder.Append($@"
                    </tbody>
                </table>
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""margin-top:14px;border-collapse:collapse;"">
                    <tr>
                        <td style=""padding:6px 0;font-size:14px;color:#64748b;"">Tạm tính</td>
                        <td align=""right"" style=""padding:6px 0;font-size:14px;color:#0f172a;"">{FormatCurrency(retailOrder.SubTotal)}</td>
                    </tr>
                    <tr>
                        <td style=""padding:6px 0;font-size:14px;color:#64748b;"">Giảm giá</td>
                        <td align=""right"" style=""padding:6px 0;font-size:14px;color:#16a34a;"">-{FormatCurrency(retailOrder.TotalDiscountAmount ?? 0m)}</td>
                    </tr>
                    <tr>
                        <td style=""padding:6px 0;font-size:14px;color:#64748b;"">Thuế</td>
                        <td align=""right"" style=""padding:6px 0;font-size:14px;color:#0f172a;"">{FormatCurrency(retailOrder.TaxAmount ?? 0m)}</td>
                    </tr>
                    <tr>
                        <td style=""padding:12px 0 0;font-size:16px;color:#0f172a;font-weight:800;border-top:1px solid #e2e8f0;"">Tổng cộng</td>
                        <td align=""right"" style=""padding:12px 0 0;font-size:18px;color:#0f172a;font-weight:800;border-top:1px solid #e2e8f0;"">{FormatCurrency(retailOrder.FinalAmount)}</td>
                    </tr>
                </table>
            </div>");

        return builder.ToString();
    }

    private static string WrapEmailLayout(string title, string subtitle, string content)
    {
        return $@"<!doctype html>
<html lang=""vi"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>{Html(title, "MyGarage")}</title>
</head>
<body style=""margin:0;padding:0;background:#f3f6fb;font-family:Arial,'Helvetica Neue',Helvetica,sans-serif;color:#0f172a;"">
  <div style=""width:100%;background:#f3f6fb;padding:24px 12px;"">
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width:680px;margin:0 auto;border-collapse:collapse;"">
      <tr>
        <td>
          <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""border-collapse:separate;border-spacing:0;background:#ffffff;border:1px solid #e2e8f0;border-radius:20px;overflow:hidden;"">
            <tr>
              <td style=""padding:28px 28px 22px;background:linear-gradient(135deg,#0f172a 0%,#1e3a8a 55%,#b8892d 100%);"">
                <div style=""font-size:13px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;color:#e2e8f0;margin-bottom:10px;"">MyGarage</div>
                <div style=""font-size:30px;line-height:1.2;font-weight:800;color:#ffffff;margin-bottom:8px;"">{Html(title, "Thông báo từ MyGarage")}</div>
                <div style=""font-size:15px;line-height:1.6;color:rgba(255,255,255,.86);"">{Html(subtitle, string.Empty)}</div>
              </td>
            </tr>
            <tr>
              <td style=""padding:28px;"">
                {content}
              </td>
            </tr>
            <tr>
              <td style=""padding:18px 28px 28px;font-size:12px;line-height:1.7;color:#94a3b8;border-top:1px solid #e2e8f0;"">
                Email này được gửi tự động từ hệ thống MyGarage.<br>
                Vui lòng không bỏ qua nếu bạn cần đối soát giao dịch hoặc hỗ trợ đơn hàng.
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </div>
</body>
</html>";
    }

    private static string Html(string? value, string fallback)
    {
        return WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? fallback : value.Trim());
    }

    private static string FormatCurrency(decimal value)
    {
        return $"{value:N0} VND";
    }
    private async Task SaveChangesInTransactionAsync()
    {
        await ExecuteInTransactionAsync(async () =>
        {
            await _db.SaveChangesAsync();
        });
    }

    private async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            await action();
            if (tx != null)
            {
                await tx.CommitAsync();
            }
        });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_db.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _db.Database.BeginTransactionAsync();
    }
}
