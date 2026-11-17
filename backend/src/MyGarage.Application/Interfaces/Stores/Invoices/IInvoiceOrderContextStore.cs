using MyGarage.Models;

namespace MyGarage.Application.Invoices;

public interface IInvoiceOrderContextStore
{
    Task<List<Invoice>> GetInvoicesByServiceOrderIdAsync(long serviceOrderId, CancellationToken ct = default);
    Task<List<Invoice>> GetInvoicesByRetailOrderIdAsync(long retailOrderId, CancellationToken ct = default);
    Task<Invoice?> GetLatestInvoiceByServiceOrderIdAsync(long serviceOrderId, CancellationToken ct = default);
    Task<Invoice?> GetLatestInvoiceByRetailOrderIdAsync(long retailOrderId, CancellationToken ct = default);
    Task<ServiceOrder?> GetServiceOrderWithDetailsForInvoiceAsync(long serviceOrderId, CancellationToken ct = default);
    Task<RetailOrder?> GetRetailOrderWithItemsForInvoiceAsync(long retailOrderId, CancellationToken ct = default);
}
