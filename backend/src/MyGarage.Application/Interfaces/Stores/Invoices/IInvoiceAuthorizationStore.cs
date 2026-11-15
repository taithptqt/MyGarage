namespace MyGarage.Application.Invoices;

public interface IInvoiceAuthorizationStore : IInvoiceBranchLookupStore
{
    Task<InvoiceAuthInfo?> GetInvoiceAuthInfoAsync(long invoiceId, CancellationToken ct = default);
    Task<bool> IsServiceOrderOwnedByCustomerAsync(long serviceOrderId, long customerId, CancellationToken ct = default);
    Task<bool> IsRetailOrderOwnedByCustomerAsync(long retailOrderId, long customerId, CancellationToken ct = default);
}
