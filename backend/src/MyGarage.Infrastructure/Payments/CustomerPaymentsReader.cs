using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Payments;

namespace MyGarage.Infrastructure.Payments;

public sealed class CustomerPaymentsReader(PaymentReadDbContext db) : ICustomerPaymentsReader
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
