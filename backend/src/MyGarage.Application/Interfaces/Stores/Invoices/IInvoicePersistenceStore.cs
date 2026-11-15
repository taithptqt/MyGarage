using MyGarage.Models;

namespace MyGarage.Application.Invoices;

public interface IInvoicePersistenceStore
{
    Task AddInvoiceAndSaveAsync(Invoice invoice, CancellationToken ct = default);
    Task AddInvoiceDetailsAndSaveAsync(IEnumerable<InvoiceDetail> details, CancellationToken ct = default);
    Task SaveChangesInTransactionAsync(CancellationToken ct = default);
}
