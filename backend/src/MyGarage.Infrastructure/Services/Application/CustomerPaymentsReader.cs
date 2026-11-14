using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Payments;
using MyGarage.Interfaces;

namespace MyGarage.Services;

// Transitional EF adapter: GarageContext remains in the Web project until its migration.
public sealed class CustomerPaymentsReader(IGarageContext db) : ICustomerPaymentsReader
{
    public async Task<(IReadOnlyList<CustomerPaymentRow> Items, int Total)> ReadAsync(
        long customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Payments.AsNoTracking()
            .Where(p => p.PayerId == customerId && p.PayerType != null && p.PayerType.ToUpper() == "CUSTOMER");
        var total = await query.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= total) return (Array.Empty<CustomerPaymentRow>(), total);
        var items = await query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((int)offset).Take(pageSize)
            .Select(p => new CustomerPaymentRow(p.Id, p.ShiftClosingId, p.CashierId, p.ServiceOrderId,
                p.InvoiceId, p.RetailOrderId, p.PayerType, p.PayerId, p.MethodId, p.Amount,
                p.TransactionCode, p.Type, p.Status, p.ReversedPaymentId, p.CreatedAt, p.UpdatedAt, p.UpdatedBy))
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
