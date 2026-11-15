using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Payments;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class InvoiceListPageReader(IGarageContext context) : IInvoiceListPageReader
{
    public async Task<InvoiceListPage> ReadAsync(InvoiceListCriteria criteria, CancellationToken cancellationToken)
    {
        var invoices = context.Invoices.AsNoTracking();
        if (criteria.BranchId.HasValue)
        {
            var branchId = criteria.BranchId.Value;
            invoices = invoices.Where(x => (x.ServiceOrderId.HasValue && x.ServiceOrder != null && x.ServiceOrder.ExecuteBranchId == branchId) ||
                (!x.ServiceOrderId.HasValue && x.RetailOrder != null && x.RetailOrder.BranchId == branchId));
        }
        // SQL Server ignores trailing spaces in string equality. The sentinel makes
        // them significant, matching the legacy JavaScript status comparison.
        if (criteria.Status != "ALL")
        {
            if (criteria.Status == "UNKNOWN")
                invoices = invoices.Where(x => x.PaymentStatus == null || x.PaymentStatus + "|" == "|" || x.PaymentStatus.ToUpper() + "|" == "UNKNOWN|");
            else
                invoices = invoices.Where(x => x.PaymentStatus != null && x.PaymentStatus.ToUpper() + "|" == criteria.Status + "|");
        }
        if (criteria.Source == "SERVICE") invoices = invoices.Where(x => x.ServiceOrderId.HasValue && x.ServiceOrderId != 0);
        else if (criteria.Source == "RETAIL") invoices = invoices.Where(x => x.RetailOrderId.HasValue && x.RetailOrderId != 0);
        else if (criteria.Source != "ALL") invoices = invoices.Where(x => false);

        if (criteria.Search.Length > 0)
        {
            var text = invoices.Select(x => new
            {
                Invoice = x,
                SearchText = x.Id.ToString() + " " +
                    (x.ServiceOrderId.HasValue && x.ServiceOrderId != 0 ? "SO-" + x.ServiceOrderId.Value.ToString() :
                     x.RetailOrderId.HasValue && x.RetailOrderId != 0 ? "RO-" + x.RetailOrderId.Value.ToString() : "N/A") + " " +
                    (x.CompanyName ?? "") + " " + (x.ReceiveEmail ?? "") + " " +
                    (x.ServiceOrderId.HasValue ? x.ServiceOrderId.Value.ToString() : "") + " " +
                    (x.RetailOrderId.HasValue ? x.RetailOrderId.Value.ToString() : "")
            });
            // JavaScript's includes is accent-sensitive; avoid SQL Server's default accent-insensitive collation.
            invoices = context.Database.IsSqlServer()
                ? text.Where(x => EF.Functions.Collate(x.SearchText.ToLower(), "Latin1_General_100_BIN2").Contains(criteria.Search)).Select(x => x.Invoice)
                : text.Where(x => x.SearchText.ToLower().Contains(criteria.Search)).Select(x => x.Invoice);
        }
        var stats = await invoices.GroupBy(_ => 1).Select(group => new
        {
            Total = group.Count(),
            Issued = group.Count(x => x.IssuedAt.HasValue),
            Paid = group.Count(x => x.PaymentStatus != null && x.PaymentStatus.ToUpper() + "|" == "PAID|"),
            Unknown = group.Count(x => x.PaymentStatus == null || x.PaymentStatus + "|" == "|" || x.PaymentStatus.ToUpper() + "|" == "UNKNOWN|")
        }).FirstOrDefaultAsync(cancellationToken);
        var total = stats?.Total ?? 0;
        var offset = ((long)criteria.Page - 1) * criteria.PageSize;
        var items = offset >= total ? new List<InvoiceListItem>() : await invoices.OrderByDescending(x => x.Id)
            .Skip((int)offset).Take(criteria.PageSize)
            .Select(x => new InvoiceListItem(x.Id, x.ServiceOrderId, x.RetailOrderId, x.InvoiceUrl, x.CompanyName,
                x.CompanyAddress, x.ReceiveEmail, x.TaxCode, x.TaxAmount, x.TotalAmount, x.PaidAmount,
                x.PaymentStatus, x.CreditNote, x.IssuedAt)).ToListAsync(cancellationToken);
        return new InvoiceListPage(items, total, criteria.Page, criteria.PageSize,
            new InvoiceListSummary(total, stats?.Issued ?? 0, stats?.Paid ?? 0, stats?.Unknown ?? 0));
    }
}
