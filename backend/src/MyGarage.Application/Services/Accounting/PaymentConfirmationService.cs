using System.Net;
using System.Text;
using MyGarage.Application.Payments;
using MyGarage.Domain.Payments;
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
    private readonly IPaymentStore _paymentStore;
    private readonly IInvoiceService _invoiceService;
    private readonly IEmailService _emailService;

    public PaymentConfirmationService(
        IPaymentStore paymentStore,
        IInvoiceService invoiceService,
        IEmailService emailService)
    {
        _paymentStore = paymentStore;
        _invoiceService = invoiceService;
        _emailService = emailService;
    }

    public async Task ApplyOutcomeAsync(Payment payment, decimal amountPaid, string status)
    {
        if (payment == null || payment.Id <= 0)
            throw new InvalidOperationException("Giao dịch thanh toán không hợp lệ.");

        var normalizedStatus = NormalizeStatus(status);
        var updatedBy = payment.UpdatedBy;
        var previousStatus = string.Empty;
        var settled = await _paymentStore.ExecuteSettlementAsync(payment.Id, async current =>
        {
            previousStatus = (current.Status ?? string.Empty).ToUpperInvariant();
            current.Amount = amountPaid;
            current.Status = normalizedStatus;
            current.UpdatedAt = VietnamTime.Now;
            if (updatedBy.HasValue) current.UpdatedBy = updatedBy;
            await _paymentStore.SaveChangesInTransactionAsync();
            await UpdateRelatedDocumentsAsync(current);
        });

        // Keep callers holding the original instance consistent after the tracked row was reloaded.
        payment.Amount = settled.Amount;
        payment.Status = settled.Status;
        payment.InvoiceId = settled.InvoiceId;
        payment.Direction = settled.Direction;
        payment.UpdatedAt = settled.UpdatedAt;
        payment.UpdatedBy = settled.UpdatedBy;

        if (previousStatus != PaymentStatusCompleted && normalizedStatus == PaymentStatusCompleted)
        {
            await TrySendPaymentSuccessEmailAsync(settled);
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
            var invoice = await _paymentStore.GetInvoiceForUpdateAsync(payment.InvoiceId.Value);
            if (invoice is not null)
            {
                var paidAmount = await _paymentStore.GetCompletedPaymentsSumForInvoiceAsync(payment.InvoiceId.Value);

                invoice.PaidAmount = paidAmount;
                var invoiceTotal = invoice.TotalAmount ?? 0m;
                invoice.PaymentStatus = DocumentPaymentStatusRules.Resolve(paidAmount, invoiceTotal);
                invoice.UpdatedAt = VietnamTime.Now;
            }
        }

        if (payment.ServiceOrderId.HasValue)
        {
            var serviceOrder = await _paymentStore.GetServiceOrderForUpdateAsync(payment.ServiceOrderId.Value);
            if (serviceOrder is not null)
            {
                var paidAmount = await _paymentStore.GetCompletedPaymentsSumForServiceOrderAsync(payment.ServiceOrderId.Value);

                var serviceTotal = serviceOrder.FinalAmount ?? 0m;
                serviceOrder.PaymentStatus = DocumentPaymentStatusRules.Resolve(paidAmount, serviceTotal);
                serviceOrder.UpdatedAt = VietnamTime.Now;
            }
        }

        long? confirmedRetailOrderId = null;
        if (payment.RetailOrderId.HasValue)
        {
            var retailOrder = await _paymentStore.GetRetailOrderForUpdateAsync(payment.RetailOrderId.Value);
            if (retailOrder is not null)
            {
                var paidAmount = await _paymentStore.GetCompletedPaymentsSumForRetailOrderAsync(payment.RetailOrderId.Value);

                // Retail order status follows existing DB check constraint.
                retailOrder.Status = paidAmount >= retailOrder.FinalAmount ? RetailOrderStatusConfirmed : RetailOrderStatusDraft;
                retailOrder.UpdatedAt = VietnamTime.Now;
                if (string.Equals(retailOrder.Status, RetailOrderStatusConfirmed, StringComparison.OrdinalIgnoreCase))
                {
                    confirmedRetailOrderId = retailOrder.Id;
                }
            }
        }

        await _paymentStore.SaveChangesInTransactionAsync();

        if (confirmedRetailOrderId.HasValue)
        {
            await _paymentStore.EnsureRetailOrderStockDeductedAsync(confirmedRetailOrderId.Value);
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

        // Standalone payments do not have an order from which an invoice can be
        // resolved. Preserve their existing lifecycle without inventing a link.
        if (payment.InvoiceId is not > 0 && payment.ServiceOrderId is not > 0 && payment.RetailOrderId is not > 0)
        {
            return;
        }

        // A caller may already have selected a specific invoice. Do not replace a
        // verified link with the latest invoice for the same order: that invoice
        // may belong to a different payer.
        Invoice? invoice;
        if (payment.InvoiceId is > 0)
        {
            invoice = await _paymentStore.GetInvoiceForUpdateAsync(payment.InvoiceId.Value);
            if (invoice == null || !IsVerifiedInvoiceLink(payment, invoice))
                throw new InvalidOperationException("Hóa đơn đã chọn không khớp nguồn hoặc bên thanh toán; cần đối soát.");
        }
        else
        {
            invoice = await _invoiceService.EnsureForPaymentContextAsync(
                payment.ServiceOrderId,
                payment.RetailOrderId,
                payment.PayerType,
                payment.PayerId);
            if (invoice == null || !IsVerifiedInvoiceLink(payment, invoice))
                throw new InvalidOperationException("Không xác định được hóa đơn phù hợp; cần đối soát thanh toán.");
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
            await _paymentStore.SaveChangesInTransactionAsync();
        }
    }

    private static bool IsVerifiedInvoiceLink(Payment payment, Invoice? invoice)
    {
        if (invoice == null || payment.PayerId is not > 0 || invoice.PayerId != payment.PayerId ||
            string.IsNullOrWhiteSpace(payment.PayerType) ||
            !string.Equals(payment.PayerType.Trim(), invoice.PayerType?.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        // Require a source on the payment and reject partially matching mixed sources.
        if (payment.ServiceOrderId is not > 0 && payment.RetailOrderId is not > 0)
            return false;
        var paymentServiceOrder = payment.ServiceOrderId is > 0 ? payment.ServiceOrderId : null;
        var paymentRetailOrder = payment.RetailOrderId is > 0 ? payment.RetailOrderId : null;
        var invoiceServiceOrder = invoice.ServiceOrderId is > 0 ? invoice.ServiceOrderId : null;
        var invoiceRetailOrder = invoice.RetailOrderId is > 0 ? invoice.RetailOrderId : null;
        return paymentServiceOrder == invoiceServiceOrder && paymentRetailOrder == invoiceRetailOrder;
    }

    private async Task TrySendPaymentSuccessEmailAsync(Payment payment)
    {
        try
        {
            var recipient = await ResolveCustomerRecipientAsync(payment);
            if (!recipient.HasValue || string.IsNullOrWhiteSpace(recipient.Value.Email))
            {
                return;
            }

            var body = BuildPaymentSuccessEmailBody(
                recipient.Value.FullName,
                payment.TransactionCode,
                payment.Amount,
                payment.Status,
                VietnamTime.Now,
                null);

            await _emailService.SendEmailAsync(recipient.Value.Email, "MyGarage - Thanh toán thành công", body);
        }
        catch
        {
            // Email failure must not fail payment confirmation
        }
    }

    private async Task<(string Email, string FullName)?> ResolveCustomerRecipientAsync(Payment payment)
    {
        // Bước 1: Tìm qua PayerType/PayerId trực tiếp trên Payment
        if (string.Equals(payment.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) && payment.PayerId.HasValue)
        {
            var customer = await _paymentStore.GetCustomerEmailInfoAsync(payment.PayerId.Value);
            if (customer is not null && !string.IsNullOrWhiteSpace(customer.Email))
            {
                return (customer.Email!, customer.FullName ?? "Khách hàng");
            }
        }

        // Bước 2: Tìm qua ServiceOrder -> Customer
        if (payment.ServiceOrderId.HasValue)
        {
            var customer = await _paymentStore.GetServiceOrderCustomerEmailInfoAsync(payment.ServiceOrderId.Value);
            if (customer is not null && !string.IsNullOrWhiteSpace(customer.Email))
            {
                return (customer.Email!, customer.FullName ?? "Khách hàng");
            }
        }

        // Bước 3: Tìm qua RetailOrder -> Customer hoặc Invoice.ReceiveEmail
        if (payment.RetailOrderId.HasValue)
        {
            var recipient = await _paymentStore.GetRetailOrderRecipientInfoAsync(payment.RetailOrderId.Value);
            if (recipient is not null && !string.IsNullOrWhiteSpace(recipient.Email))
            {
                return (recipient.Email!, recipient.FullName ?? "Khách hàng");
            }
        }

        // Bước 4: Tìm qua in-memory OrderStore (BuyerEmail từ PayOS order)
        if (!string.IsNullOrWhiteSpace(payment.TransactionCode))
        {
            var order = _paymentStore.GetOrderBuyerInfoByPaymentLinkId(payment.TransactionCode);
            if (order is not null && !string.IsNullOrWhiteSpace(order.BuyerEmail))
            {
                return (order.BuyerEmail!, string.IsNullOrWhiteSpace(order.BuyerName) ? "Khách hàng" : order.BuyerName);
            }
        }

        // Bước 5: Tìm qua Invoice trực tiếp
        if (payment.InvoiceId.HasValue)
        {
            var invoice = await _paymentStore.GetInvoiceEmailInfoAsync(payment.InvoiceId.Value);
            if (invoice is not null &&
                string.Equals(invoice.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) &&
                invoice.PayerId.HasValue)
            {
                var customer = await _paymentStore.GetCustomerEmailInfoAsync(invoice.PayerId.Value);
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
            var customerId = await _paymentStore.GetRetailOrderCustomerIdAsync(payment.RetailOrderId.Value);
            if (customerId.HasValue)
            {
                var customer = await _paymentStore.GetCustomerEmailInfoAsync(customerId.Value);
                if (customer is not null && !string.IsNullOrWhiteSpace(customer.Email))
                {
                    return (customer.Email!, customer.FullName ?? "Khách hàng");
                }
            }
        }

        return null;
    }

    public async Task SendRetailOrderSuccessEmailAsync(
        long retailOrderId,
        decimal amountPaid,
        string transactionCode,
        string status,
        DateTimeOffset transactionTime,
        string? fallbackEmail = null,
        string? fallbackFullName = null)
    {
        try
        {
            var retailOrder = await _paymentStore.GetRetailOrderWithItemsForEmailAsync(retailOrderId);
            if (retailOrder == null)
            {
                return;
            }

            var recipientInfo = await _paymentStore.GetRetailOrderRecipientInfoAsync(retailOrderId);
            (string Email, string FullName)? recipient = recipientInfo is not null && !string.IsNullOrWhiteSpace(recipientInfo.Email)
                ? (recipientInfo.Email!, recipientInfo.FullName ?? "Khách hàng")
                : null;

            if ((!recipient.HasValue || string.IsNullOrWhiteSpace(recipient.Value.Email)) && !string.IsNullOrWhiteSpace(fallbackEmail))
            {
                recipient = (fallbackEmail!, string.IsNullOrWhiteSpace(fallbackFullName) ? "Khách hàng" : fallbackFullName.Trim());
            }

            if (!recipient.HasValue || string.IsNullOrWhiteSpace(recipient.Value.Email))
            {
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
        catch
        {
            // Email failure must not fail payment confirmation
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
}
