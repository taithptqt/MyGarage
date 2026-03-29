using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public class PaymentConfirmationService
{
    private readonly GarageContext _db;
    private readonly IEmailService _emailService;

    public PaymentConfirmationService(GarageContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    public async Task ApplyOutcomeAsync(Payment payment, decimal amountPaid, string status)
    {
        var normalizedStatus = NormalizeStatus(status);
        var previousStatus = (payment.Status ?? string.Empty).ToUpperInvariant();

        payment.Amount = amountPaid;
        payment.Status = normalizedStatus;
        payment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await UpdateRelatedDocumentsAsync(payment);

        if (previousStatus != "COMPLETED" && normalizedStatus == "COMPLETED")
        {
            await TrySendPaymentSuccessEmailAsync(payment);
        }
    }

    private static string NormalizeStatus(string? status)
    {
        var normalized = status?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "COMPLETED" => "COMPLETED",
            "CANCELED" => "CANCELED",
            "FAILED" => "FAILED",
            _ => "PENDING"
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
                    .Where(x => x.InvoiceId == payment.InvoiceId.Value && x.Status == "COMPLETED")
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                invoice.PaidAmount = paidAmount;
                var invoiceTotal = invoice.TotalAmount ?? 0m;
                invoice.PaymentStatus = invoiceTotal > 0m && paidAmount >= invoiceTotal ? "COMPLETED" : "PENDING";
                invoice.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (payment.ServiceOrderId.HasValue)
        {
            var serviceOrder = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == payment.ServiceOrderId.Value);
            if (serviceOrder is not null)
            {
                var paidAmount = await _db.Payments
                    .Where(x => x.ServiceOrderId == payment.ServiceOrderId.Value && x.Status == "COMPLETED")
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                var serviceTotal = serviceOrder.FinalAmount ?? 0m;
                serviceOrder.PaymentStatus = serviceTotal > 0m && paidAmount >= serviceTotal ? "COMPLETED" : "PENDING";
                serviceOrder.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (payment.RetailOrderId.HasValue)
        {
            var retailOrder = await _db.RetailOrders.FirstOrDefaultAsync(x => x.Id == payment.RetailOrderId.Value);
            if (retailOrder is not null)
            {
                var paidAmount = await _db.Payments
                    .Where(x => x.RetailOrderId == payment.RetailOrderId.Value && x.Status == "COMPLETED")
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                // Retail order status follows existing DB check constraint.
                retailOrder.Status = paidAmount >= retailOrder.FinalAmount ? "CONFIRMED" : "DRAFT";
                retailOrder.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
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

            var amount = payment.Amount.ToString("N0");
            var body = $@"
                <h3>MyGarage - Thanh toán thành công</h3>
                <p>Xin chào {recipient.Value.FullName},</p>
                <p>Chúng tôi đã nhận thanh toán thành công cho giao dịch <b>{payment.TransactionCode}</b>.</p>
                <ul>
                  <li>Số tiền: <b>{amount} VND</b></li>
                  <li>Trạng thái: <b>{payment.Status}</b></li>
                  <li>Thời gian: <b>{DateTime.Now:dd/MM/yyyy HH:mm:ss}</b></li>
                </ul>
                <p>Cảm ơn bạn đã sử dụng dịch vụ MyGarage.</p>";

            await _emailService.SendEmailAsync(recipient.Value.Email, "MyGarage - Thanh toán thành công", body);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PaymentConfirmationService] Failed to send payment email: {ex.Message}");
        }
    }

    private async Task<(string Email, string FullName)?> ResolveCustomerRecipientAsync(Payment payment)
    {
        if (string.Equals(payment.PayerType, "CUSTOMER", StringComparison.OrdinalIgnoreCase) && payment.PayerId.HasValue)
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == payment.PayerId.Value);
            if (customer is not null && !string.IsNullOrWhiteSpace(customer.Email))
            {
                return (customer.Email!, customer.FullName ?? "Khách hàng");
            }
        }

        if (payment.ServiceOrderId.HasValue)
        {
            var serviceOrder = await _db.ServiceOrders
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.Id == payment.ServiceOrderId.Value);
            if (serviceOrder?.Customer is not null && !string.IsNullOrWhiteSpace(serviceOrder.Customer.Email))
            {
                return (serviceOrder.Customer.Email!, serviceOrder.Customer.FullName ?? "Khách hàng");
            }
        }

        if (payment.RetailOrderId.HasValue)
        {
            var retailOrder = await _db.RetailOrders
                .Include(r => r.Customer)
                .FirstOrDefaultAsync(r => r.Id == payment.RetailOrderId.Value);
            if (retailOrder?.Customer is not null && !string.IsNullOrWhiteSpace(retailOrder.Customer.Email))
            {
                return (retailOrder.Customer.Email!, retailOrder.Customer.FullName ?? "Khách hàng");
            }
        }

        if (payment.InvoiceId.HasValue)
        {
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

        return null;
    }
}
