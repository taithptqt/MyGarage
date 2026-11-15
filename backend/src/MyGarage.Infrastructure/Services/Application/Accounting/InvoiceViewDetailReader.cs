using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Invoices;
using MyGarage.Interfaces;

namespace MyGarage.Services;

// Transitional adapter: the legacy EF context still belongs to the Web project.
public sealed class InvoiceViewDetailReader(IGarageContext db) : IInvoiceViewDetailReader
{
    public async Task<InvoiceViewDetail?> ReadAsync(long invoiceId, int detailsPage, int paymentsPage, int pageSize, CancellationToken cancellationToken, long? branchId = null)
    {
        var invoices = db.Invoices.AsNoTracking().Where(x => x.Id == invoiceId);
        if (branchId.HasValue)
            invoices = invoices.Where(x => (x.ServiceOrderId.HasValue && x.ServiceOrder != null && x.ServiceOrder.ExecuteBranchId == branchId.Value) ||
                (!x.ServiceOrderId.HasValue && x.RetailOrder != null && x.RetailOrder.BranchId == branchId.Value));
        var invoice = await invoices
            .Select(x => new InvoiceViewRow(x.Id, x.ServiceOrderId, x.RetailOrderId, x.VehicleId, x.Phone, x.Address,
                x.InvoiceUrl, x.CompanyName, x.CompanyAddress, x.ReceiveEmail, x.TaxCode, x.SubTotalAmount, x.TaxAmount,
                x.TotalAmount, x.PayerType, x.PayerId, x.PaidAmount, x.PaymentStatus, x.CreditApprovedByStaffId,
                x.CreditNote, x.IssuedAt, x.CreatedAt, x.CreatedBy, x.UpdatedAt, x.UpdatedBy)).FirstOrDefaultAsync(cancellationToken);
        if (invoice == null) return null;
        var lines = db.InvoiceDetails.AsNoTracking().Where(x => x.InvoiceId == invoiceId);
        var payments = db.Payments.AsNoTracking().Where(x => x.InvoiceId == invoiceId);
        var linesTotal = await lines.CountAsync(cancellationToken);
        var paymentsTotal = await payments.CountAsync(cancellationToken);
        var linesOffset = ((long)detailsPage - 1) * pageSize;
        var paymentsOffset = ((long)paymentsPage - 1) * pageSize;
        var lineRows = linesOffset >= linesTotal ? new List<InvoiceLineRow>() :
            await lines.OrderBy(x => x.Id).Skip((int)linesOffset).Take(pageSize)
                .Select(x => new InvoiceLineRow(x.Id, x.InvoiceId, x.ReferenceServiceOrderDetailId, x.ItemType,
                    x.ProductId, x.StandardServiceId, x.ItemName, x.Quantity, x.CostPriceAtTime, x.UnitPriceAtTime,
                    x.Unit, x.SubTotal, x.DiscountAllocatedAmount, x.TaxRate, x.TaxAmount, x.FinalAmount, x.Note))
                .ToListAsync(cancellationToken);
        var paymentRows = paymentsOffset >= paymentsTotal ? new List<InvoicePaymentRow>() :
            await payments.OrderBy(x => x.Id).Skip((int)paymentsOffset).Take(pageSize)
                .Select(x => new InvoicePaymentRow(x.Id, x.ShiftClosingId, x.CashierId, x.ServiceOrderId,
                    x.InvoiceId, x.RetailOrderId, x.PayerType, x.PayerId, x.MethodId, x.Amount, x.TransactionCode,
                    x.Type, x.Status, x.ReversedPaymentId, x.CreatedAt, x.UpdatedAt, x.UpdatedBy))
                .ToListAsync(cancellationToken);
        return new InvoiceViewDetail(invoice, lineRows, linesTotal, paymentRows, paymentsTotal, pageSize, detailsPage, paymentsPage);
    }
}
