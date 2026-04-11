using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public class PaymentConfirmationService : IPaymentConfirmationService
{
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

        _logger.LogInformation(
            "[PaymentConfirmationService] ApplyOutcomeAsync: PaymentId={PaymentId}, previousStatus={PreviousStatus}, " +
            "newStatus={NewStatus}, amountPaid={AmountPaid}, RetailOrderId={RetailOrderId}, ServiceOrderId={ServiceOrderId}",
            payment.Id, previousStatus, normalizedStatus, amountPaid,
            payment.RetailOrderId, payment.ServiceOrderId);

        // Chỉ cập nhật nếu status thực sự thay đổi hoặc amount thay đổi
        var statusChanged = previousStatus != normalizedStatus;
        var amountChanged = payment.Amount != amountPaid;

        if (statusChanged || amountChanged)
        {
            payment.Amount = amountPaid;
            payment.Status = normalizedStatus;
            payment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        await UpdateRelatedDocumentsAsync(payment);

        // FIX: Gửi email khi status cuối cùng là COMPLETED.
        // Trước đây: chỉ gửi khi previousStatus != COMPLETED → bị skip nếu webhook đã set COMPLETED trước.
        // Bây giờ: gửi nếu (1) đang transition sang COMPLETED, HOẶC (2) đã COMPLETED nhưng chưa gửi email.
        // Dùng heuristic: nếu previousStatus đã là COMPLETED VÀ status vẫn COMPLETED → có thể webhook đã xử lý.
        // Tuy nhiên, webhook KHÔNG gửi email (chỉ set status), nên syncOrder cần gửi email.
        if (normalizedStatus == nameof(PaymentStatusCode.COMPLETED))
        {
            if (previousStatus != nameof(PaymentStatusCode.COMPLETED))
            {
                // Transition mới từ non-COMPLETED → COMPLETED → luôn gửi
                _logger.LogInformation(
                    "[PaymentConfirmationService] Transition {PreviousStatus} → COMPLETED cho Payment #{PaymentId}. Gửi email.",
                    previousStatus, payment.Id);
                await TrySendPaymentSuccessEmailAsync(payment);
            }
            else
            {
                // previousStatus đã là COMPLETED (webhook chạy trước hoặc retry).
                // Vẫn gửi email vì webhook chỉ set status, không gửi email.
                _logger.LogInformation(
                    "[PaymentConfirmationService] Payment #{PaymentId} đã COMPLETED (có thể do webhook). " +
                    "Retry gửi email để đảm bảo khách nhận được hoá đơn.",
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
            nameof(PaymentStatusCode.COMPLETED) => nameof(PaymentStatusCode.COMPLETED),
            "PAID" => nameof(PaymentStatusCode.COMPLETED),
            "SUCCESS" => nameof(PaymentStatusCode.COMPLETED),
            "SUCCEEDED" => nameof(PaymentStatusCode.COMPLETED),
            nameof(PaymentStatusCode.CANCELED) => nameof(PaymentStatusCode.CANCELED),
            "CANCELLED" => nameof(PaymentStatusCode.CANCELED),
            nameof(PaymentStatusCode.FAILED) => nameof(PaymentStatusCode.FAILED),
            _ => nameof(PaymentStatusCode.PENDING)
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
                    .Where(x => x.InvoiceId == payment.InvoiceId.Value && x.Status == nameof(PaymentStatusCode.COMPLETED))
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                invoice.PaidAmount = paidAmount;
                var invoiceTotal = invoice.TotalAmount ?? 0m;
                invoice.PaymentStatus = invoiceTotal > 0m && paidAmount >= invoiceTotal
                    ? nameof(PaymentDocumentStatusCode.PAID)
                    : (paidAmount > 0m ? nameof(PaymentDocumentStatusCode.PARTIAL) : nameof(PaymentDocumentStatusCode.UNPAID));
                invoice.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (payment.ServiceOrderId.HasValue)
        {
            var serviceOrder = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == payment.ServiceOrderId.Value);
            if (serviceOrder is not null)
            {
                var paidAmount = await _db.Payments
                    .Where(x => x.ServiceOrderId == payment.ServiceOrderId.Value && x.Status == nameof(PaymentStatusCode.COMPLETED))
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                var serviceTotal = serviceOrder.FinalAmount ?? 0m;
                serviceOrder.PaymentStatus = serviceTotal > 0m && paidAmount >= serviceTotal
                    ? nameof(PaymentDocumentStatusCode.PAID)
                    : (paidAmount > 0m ? nameof(PaymentDocumentStatusCode.PARTIAL) : nameof(PaymentDocumentStatusCode.UNPAID));
                serviceOrder.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (payment.RetailOrderId.HasValue)
        {
            var retailOrder = await _db.RetailOrders.FirstOrDefaultAsync(x => x.Id == payment.RetailOrderId.Value);
            if (retailOrder is not null)
            {
                var paidAmount = await _db.Payments
                    .Where(x => x.RetailOrderId == payment.RetailOrderId.Value && x.Status == nameof(PaymentStatusCode.COMPLETED))
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                // Retail order status follows existing DB check constraint.
                retailOrder.Status = paidAmount >= retailOrder.FinalAmount ? nameof(RetailOrderStatus.CONFIRMED) : nameof(RetailOrderStatus.DRAFT);
                retailOrder.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
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
                    "[PaymentConfirmationService] ❌ Không tìm được email khách hàng cho payment #{PaymentId} " +
                    "(RetailOrderId={RetailOrderId}, ServiceOrderId={ServiceOrderId}, PayerType={PayerType}, PayerId={PayerId}). " +
                    "Email bị bỏ qua. Hãy kiểm tra: (1) Customer có email không? (2) BuyerEmail trong Order? (3) Invoice.ReceiveEmail?",
                    payment.Id, payment.RetailOrderId, payment.ServiceOrderId, payment.PayerType, payment.PayerId);
                return;
            }

            _logger.LogInformation(
                "[PaymentConfirmationService] ✅ Đã tìm được email: {Email} ({FullName}) cho Payment #{PaymentId}",
                recipient.Value.Email, recipient.Value.FullName, payment.Id);

            var amount = payment.Amount.ToString("N0");
            var body = $@"
<div style=""font-family: Arial, sans-serif; background-color: #f4f7f6; padding: 20px; color: #333;"">
    <div style=""max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);"">
        <!-- Header -->
        <div style=""background-color: #0056b3; color: #ffffff; padding: 20px; text-align: center;"">
            <h2 style=""margin: 0; font-size: 24px;"">MyGarage</h2>
            <p style=""margin: 5px 0 0; font-size: 16px; opacity: 0.9;"">Hóa Đơn / Xác nhận thanh toán</p>
        </div>

        <!-- Content -->
        <div style=""padding: 20px; line-height: 1.6;"">
            <p>Xin chào <strong>{recipient.Value.FullName}</strong>,</p>
            <p>Tuyệt vời! Chúng tôi đã xác nhận khoản thanh toán thành công cho giao dịch <b>{payment.TransactionCode}</b> của bạn.</p>

            <div style=""background-color: #f8f9fa; border-left: 4px solid #28a745; padding: 10px 15px; margin: 20px 0;"">
                <p style=""margin: 0;""><strong>Số tiền:</strong> <span style=""color: #d9534f; font-weight: bold; font-size: 16px;"">{amount} VND</span></p>
                <p style=""margin: 5px 0 0;""><strong>Trạng thái:</strong> <span style=""color: #28a745;"">Thành công ({payment.Status})</span></p>
                <p style=""margin: 5px 0 0;""><strong>Thời gian:</strong> {payment.UpdatedAt ?? DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}</p>
            </div>";

            if (payment.RetailOrderId.HasValue)
            {
                var retailOrder = await _db.RetailOrders
                    .Include(r => r.RetailOrderItems)
                    .ThenInclude(ri => ri.Product)
                    .FirstOrDefaultAsync(r => r.Id == payment.RetailOrderId.Value);
                
                if (retailOrder != null && retailOrder.RetailOrderItems.Any())
                {
                    body += $@"
            <h4 style=""border-bottom: 2px solid #eeeeee; padding-bottom: 8px; margin-top: 30px; color: #0056b3;"">
                Chi tiết mua hàng (Mã đơn: {retailOrder.OrderCode})
            </h4>
            <table style=""width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 14px;"">
                <thead>
                    <tr style=""background-color: #f1f1f1; text-align: left;"">
                        <th style=""padding: 10px; border-bottom: 1px solid #ddd;"">Sản phẩm</th>
                        <th style=""padding: 10px; border-bottom: 1px solid #ddd; text-align: center;"">SL</th>
                        <th style=""padding: 10px; border-bottom: 1px solid #ddd; text-align: right;"">Đơn giá</th>
                        <th style=""padding: 10px; border-bottom: 1px solid #ddd; text-align: right;"">Thành tiền</th>
                    </tr>
                </thead>
                <tbody>";

                    foreach (var item in retailOrder.RetailOrderItems)
                    {
                        var prodName = item.Product?.Name ?? "Sản phẩm";
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
                        <td colspan=""3"" style=""padding: 10px 10px 5px; text-align: right; color: #555;"">Tạm tính:</td>
                        <td style=""padding: 10px 10px 5px; text-align: right;"">{retailOrder.SubTotal:N0} ₫</td>
                    </tr>
                    <tr>
                        <td colspan=""3"" style=""padding: 5px 10px; text-align: right; color: #555;"">Giảm giá:</td>
                        <td style=""padding: 5px 10px; text-align: right; color: #28a745;"">-{(retailOrder.TotalDiscountAmount ?? 0):N0} ₫</td>
                    </tr>
                    <tr>
                        <td colspan=""3"" style=""padding: 5px 10px; text-align: right; color: #555;"">Thuế:</td>
                        <td style=""padding: 5px 10px; text-align: right;"">+{(retailOrder.TaxAmount ?? 0):N0} ₫</td>
                    </tr>
                    <tr>
                        <td colspan=""3"" style=""padding: 15px 10px; text-align: right; font-weight: bold; font-size: 16px; border-top: 2px solid #ddd;"">TỔNG CỘNG:</td>
                        <td style=""padding: 15px 10px; text-align: right; font-weight: bold; font-size: 16px; color: #d9534f; border-top: 2px solid #ddd;"">{retailOrder.FinalAmount:N0} ₫</td>
                    </tr>
                </tfoot>
            </table>";
                }
            }

            body += $@"
            <p style=""margin-top: 30px;"">Nếu bạn có bất kỳ câu hỏi nào, vui lòng liên hệ với chúng tôi qua số điện thoại hỗ trợ hoặc phản hồi trực tiếp qua email này.</p>
            <p>Trân trọng,<br><strong>Đội ngũ MyGarage</strong></p>
        </div>

        <!-- Footer -->
        <div style=""background-color: #f1f5f9; padding: 15px; text-align: center; font-size: 12px; color: #888;"">
            <p style=""margin: 0;"">© {DateTime.Now.Year} MyGarage. All rights reserved.</p>
            <p style=""margin: 5px 0 0;"">Email này được gửi tự động từ hệ thống. Vui lòng không trả lời bằng email rác.</p>
        </div>
    </div>
</div>";

            var subject = payment.RetailOrderId.HasValue ? "MyGarage - Thanh toán thành công & Hóa đơn mua hàng" : "MyGarage - Thanh toán thành công";
            await _emailService.SendEmailAsync(recipient.Value.Email, subject, body);
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
                _logger.LogDebug("[ResolveEmail] Bước 1: ✅ Tìm thấy email={Email} từ Customer #{CustomerId}", customer.Email, customer.Id);
                return (customer.Email!, customer.FullName ?? "Khách hàng");
            }
            _logger.LogDebug("[ResolveEmail] Bước 1: Customer #{PayerId} không có email hoặc không tồn tại", payment.PayerId.Value);
        }

        // Bước 2: Tìm qua ServiceOrder → Customer
        if (payment.ServiceOrderId.HasValue)
        {
            _logger.LogDebug("[ResolveEmail] Bước 2: Tìm qua ServiceOrderId={ServiceOrderId}", payment.ServiceOrderId.Value);
            var serviceOrder = await _db.ServiceOrders
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.Id == payment.ServiceOrderId.Value);
            if (serviceOrder?.Customer is not null && !string.IsNullOrWhiteSpace(serviceOrder.Customer.Email))
            {
                _logger.LogDebug("[ResolveEmail] Bước 2: ✅ Tìm thấy email={Email} từ ServiceOrder", serviceOrder.Customer.Email);
                return (serviceOrder.Customer.Email!, serviceOrder.Customer.FullName ?? "Khách hàng");
            }
            _logger.LogDebug("[ResolveEmail] Bước 2: ServiceOrder không có Customer.Email");
        }

        // Bước 3: Tìm qua RetailOrder → Customer hoặc Invoice.ReceiveEmail
        if (payment.RetailOrderId.HasValue)
        {
            _logger.LogDebug("[ResolveEmail] Bước 3: Tìm qua RetailOrderId={RetailOrderId}", payment.RetailOrderId.Value);
            var retailOrder = await _db.RetailOrders
                .Include(r => r.Customer)
                .Include(r => r.Invoices)
                .FirstOrDefaultAsync(r => r.Id == payment.RetailOrderId.Value);
            if (retailOrder?.Customer is not null && !string.IsNullOrWhiteSpace(retailOrder.Customer.Email))
            {
                _logger.LogDebug("[ResolveEmail] Bước 3: ✅ Tìm thấy email={Email} từ RetailOrder.Customer", retailOrder.Customer.Email);
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
                _logger.LogDebug("[ResolveEmail] Bước 3b: ✅ Tìm thấy ReceiveEmail={Email} từ Invoice", invoiceReceiveEmail);
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
                _logger.LogDebug("[ResolveEmail] Bước 4: ✅ Tìm thấy BuyerEmail={Email} từ OrderStore", order.BuyerEmail);
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
                    _logger.LogDebug("[ResolveEmail] Bước 5: ✅ Tìm thấy email={Email} từ Invoice.PayerId", customer.Email);
                    return (customer.Email!, customer.FullName ?? "Khách hàng");
                }
            }

            if (invoice is not null && !string.IsNullOrWhiteSpace(invoice.ReceiveEmail))
            {
                _logger.LogDebug("[ResolveEmail] Bước 5b: ✅ Tìm thấy ReceiveEmail={Email} từ Invoice", invoice.ReceiveEmail);
                return (invoice.ReceiveEmail!, "Khách hàng");
            }
        }

        // Bước 6 (MỚI): Nếu có RetailOrderId, thử tìm Customer trực tiếp qua RetailOrder.CustomerId
        // (fallback khi Customer.Email null nhưng vẫn có thể tìm User account)
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
                    _logger.LogDebug("[ResolveEmail] Bước 6: ✅ Fallback tìm thấy email={Email} từ CustomerId={CustomerId}",
                        customer.Email, customer.Id);
                    return (customer.Email!, customer.FullName ?? "Khách hàng");
                }
            }
        }

        _logger.LogWarning("[ResolveEmail] ❌ Không tìm được email qua tất cả 6 bước cho Payment #{PaymentId}", payment.Id);
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

            var amount = amountPaid.ToString("N0");
            var body = $@"
<div style=""font-family: Arial, sans-serif; background-color: #f4f7f6; padding: 20px; color: #333;"">
    <div style=""max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.1);"">
        <!-- Header -->
        <div style=""background-color: #0056b3; color: #ffffff; padding: 20px; text-align: center;"">
            <h2 style=""margin: 0; font-size: 24px;"">MyGarage</h2>
            <p style=""margin: 5px 0 0; font-size: 16px; opacity: 0.9;"">Hóa Đơn / Xác nhận thanh toán</p>
        </div>

        <!-- Content -->
        <div style=""padding: 20px; line-height: 1.6;"">
            <p>Xin chào <strong>{recipient.Value.FullName}</strong>,</p>
            <p>Tuyệt vời! Chúng tôi đã xác nhận khoản thanh toán thành công cho giao dịch <b>{transactionCode}</b> của bạn.</p>

            <div style=""background-color: #f8f9fa; border-left: 4px solid #28a745; padding: 10px 15px; margin: 20px 0;"">
                <p style=""margin: 0;""><strong>Số tiền:</strong> <span style=""color: #d9534f; font-weight: bold; font-size: 16px;"">{amount} VND</span></p>
                <p style=""margin: 5px 0 0;""><strong>Trạng thái:</strong> <span style=""color: #28a745;"">Thành công ({status})</span></p>
                <p style=""margin: 5px 0 0;""><strong>Thời gian:</strong> {transactionTime:dd/MM/yyyy HH:mm:ss}</p>
            </div>";

            if (retailOrder.RetailOrderItems.Any())
            {
                body += $@"
            <h4 style=""border-bottom: 2px solid #eeeeee; padding-bottom: 8px; margin-top: 30px; color: #0056b3;"">
                Chi tiết mua hàng (Mã đơn: {retailOrder.OrderCode})
            </h4>
            <table style=""width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 14px;"">
                <thead>
                    <tr style=""background-color: #f1f1f1; text-align: left;"">
                        <th style=""padding: 10px; border-bottom: 1px solid #ddd;"">Sản phẩm</th>
                        <th style=""padding: 10px; border-bottom: 1px solid #ddd; text-align: center;"">SL</th>
                        <th style=""padding: 10px; border-bottom: 1px solid #ddd; text-align: right;"">Đơn giá</th>
                        <th style=""padding: 10px; border-bottom: 1px solid #ddd; text-align: right;"">Thành tiền</th>
                    </tr>
                </thead>
                <tbody>";

                foreach (var item in retailOrder.RetailOrderItems)
                {
                    var prodName = item.Product?.Name ?? "Sản phẩm";
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
                        <td colspan=""3"" style=""padding: 10px 10px 5px; text-align: right; color: #555;"">Tạm tính:</td>
                        <td style=""padding: 10px 10px 5px; text-align: right;"">{retailOrder.SubTotal:N0} ₫</td>
                    </tr>
                    <tr>
                        <td colspan=""3"" style=""padding: 5px 10px; text-align: right; color: #555;"">Giảm giá:</td>
                        <td style=""padding: 5px 10px; text-align: right; color: #28a745;"">-{(retailOrder.TotalDiscountAmount ?? 0):N0} ₫</td>
                    </tr>
                    <tr>
                        <td colspan=""3"" style=""padding: 5px 10px; text-align: right; color: #555;"">Thuế:</td>
                        <td style=""padding: 5px 10px; text-align: right;"">+{(retailOrder.TaxAmount ?? 0):N0} ₫</td>
                    </tr>
                    <tr>
                        <td colspan=""3"" style=""padding: 15px 10px; text-align: right; font-weight: bold; font-size: 16px; border-top: 2px solid #ddd;"">TỔNG CỘNG:</td>
                        <td style=""padding: 15px 10px; text-align: right; font-weight: bold; font-size: 16px; color: #d9534f; border-top: 2px solid #ddd;"">{retailOrder.FinalAmount:N0} ₫</td>
                    </tr>
                </tfoot>
            </table>";
            }

            body += $@"
            <p style=""margin-top: 30px;"">Nếu bạn có bất kỳ câu hỏi nào, vui lòng liên hệ với chúng tôi qua số điện thoại hỗ trợ hoặc phản hồi trực tiếp qua email này.</p>
            <p>Trân trọng,<br><strong>Đội ngũ MyGarage</strong></p>
        </div>

        <!-- Footer -->
        <div style=""background-color: #f1f5f9; padding: 15px; text-align: center; font-size: 12px; color: #888;"">
            <p style=""margin: 0;"">© {System.DateTime.UtcNow.Year} MyGarage. All rights reserved.</p>
            <p style=""margin: 5px 0 0;"">Email này được gửi tự động từ hệ thống. Vui lòng không trả lời bằng email rác.</p>
        </div>
    </div>
</div>";

            await _emailService.SendEmailAsync(recipient.Value.Email, "MyGarage - Thanh toán thành công & Hóa đơn mua hàng", body);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "[PaymentConfirmationService] Gửi retail email thất bại cho RetailOrderId #{RetailOrderId}", retailOrderId);
        }
    }
}

