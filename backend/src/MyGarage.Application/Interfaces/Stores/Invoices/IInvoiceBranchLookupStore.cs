namespace MyGarage.Application.Invoices;

public interface IInvoiceBranchLookupStore
{
    Task<long?> GetBranchIdForServiceOrderAsync(long serviceOrderId, CancellationToken ct = default);
    Task<long?> GetBranchIdForRetailOrderAsync(long retailOrderId, CancellationToken ct = default);
}
