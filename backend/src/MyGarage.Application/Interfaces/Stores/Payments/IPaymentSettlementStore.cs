using MyGarage.Models;

namespace MyGarage.Application.Payments;

public interface IPaymentSettlementStore
{
    Task<Invoice?> GetInvoiceForUpdateAsync(long invoiceId, CancellationToken ct = default);
    Task<decimal> GetCompletedPaymentsSumForInvoiceAsync(long invoiceId, CancellationToken ct = default);
    Task<ServiceOrder?> GetServiceOrderForUpdateAsync(long serviceOrderId, CancellationToken ct = default);
    Task<decimal> GetCompletedPaymentsSumForServiceOrderAsync(long serviceOrderId, CancellationToken ct = default);
    Task<RetailOrder?> GetRetailOrderForUpdateAsync(long retailOrderId, CancellationToken ct = default);
    Task<decimal> GetCompletedPaymentsSumForRetailOrderAsync(long retailOrderId, CancellationToken ct = default);
    Task EnsureRetailOrderStockDeductedAsync(long retailOrderId, CancellationToken ct = default);
}
