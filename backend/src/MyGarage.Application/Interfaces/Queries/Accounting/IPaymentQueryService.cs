namespace MyGarage.Application.Interfaces.Queries;

public interface IPaymentQueryService
{
    Task<long?> GetServiceOrderExecuteBranchIdAsync(long serviceOrderId, CancellationToken ct = default);
    Task<long?> GetInvoiceServiceOrderIdAsync(long invoiceId, CancellationToken ct = default);
}
