using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Payments;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class InvoiceTaxSummaryReader(IGarageContext context) : IInvoiceTaxSummaryReader
{
    public async Task<InvoiceTaxSummary> ReadAsync(CancellationToken cancellationToken, long? branchId = null)
    {
        var invoices = context.Invoices.AsNoTracking();
        if (branchId.HasValue)
            invoices = invoices.Where(x => (x.ServiceOrderId.HasValue && x.ServiceOrder != null && x.ServiceOrder.ExecuteBranchId == branchId.Value) ||
                (!x.ServiceOrderId.HasValue && x.RetailOrder != null && x.RetailOrder.BranchId == branchId.Value));
        var totals = await invoices.GroupBy(_ => 1).Select(group => new
        {
            TotalTax = group.Sum(x => x.TaxAmount ?? 0m),
            Count = group.Count(x => x.TaxAmount.HasValue && x.TaxAmount.Value != 0m)
        }).FirstOrDefaultAsync(cancellationToken);
        var taxed = invoices.Where(x => x.TaxAmount.HasValue && x.TaxAmount.Value != 0m);
        var items = await taxed.OrderByDescending(x => x.Id).Take(30)
            .Select(x => new InvoiceTaxSummaryItem(x.Id, x.ServiceOrderId, x.RetailOrderId, x.TaxAmount, x.IssuedAt))
            .ToListAsync(cancellationToken);
        return new InvoiceTaxSummary(totals?.TotalTax ?? 0m, totals?.Count ?? 0, items);
    }
}
