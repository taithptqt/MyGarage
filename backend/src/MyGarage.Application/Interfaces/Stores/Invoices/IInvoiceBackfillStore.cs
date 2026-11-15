using MyGarage.Models;

namespace MyGarage.Application.Invoices;

public interface IInvoiceBackfillStore
{
    Task<List<Payment>> GetCompletedPaymentsForBackfillAsync(CancellationToken ct = default);
    Task<List<long>> GetAllInvoiceIdsAsync(CancellationToken ct = default);
}
