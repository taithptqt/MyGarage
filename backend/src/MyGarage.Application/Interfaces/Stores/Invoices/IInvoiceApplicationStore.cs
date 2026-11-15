using MyGarage.Models;

namespace MyGarage.Application.Invoices;

public interface IInvoiceApplicationStore : IInvoiceBranchLookupStore
{
    Task<Invoice?> GetInvoiceWithDetailsAsync(long id, CancellationToken ct = default);
    Task<long?> GetServiceOrderIdByInvoiceIdAsync(long invoiceId, CancellationToken ct = default);
    Task CreateInvoiceWithDetailsAsync(Invoice invoice, IReadOnlyList<InvoiceDetail> details, CancellationToken ct = default);
}
