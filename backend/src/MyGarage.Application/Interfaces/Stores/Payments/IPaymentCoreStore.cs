using MyGarage.Models;

namespace MyGarage.Application.Payments;

public interface IPaymentCoreStore
{
    Task<Payment?> GetPaymentByIdAsync(long id, bool track, CancellationToken ct = default);
    Task<long?> GetBranchIdForServiceOrderAsync(long serviceOrderId, CancellationToken ct = default);
    Task<long?> GetBranchIdForRetailOrderAsync(long retailOrderId, CancellationToken ct = default);
    Task AddPaymentAsync(Payment payment, CancellationToken ct = default);
    Task SaveChangesInTransactionAsync(CancellationToken ct = default);
    Task<Payment> ExecuteSettlementAsync(long paymentId, Func<Payment, Task> action, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetCustomerPaymentsAsync(long customerId, CancellationToken ct = default);
}
