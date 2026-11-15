namespace MyGarage.Application.Payments;

public interface IPaymentAuthorizationStore
{
    Task<PaymentAuthInfo?> GetPaymentAuthInfoAsync(long paymentId, CancellationToken ct = default);
    Task<bool> IsServiceOrderOwnedByCustomerAsync(long serviceOrderId, long customerId, CancellationToken ct = default);
    Task<bool> IsRetailOrderOwnedByCustomerAsync(long retailOrderId, long customerId, CancellationToken ct = default);
}
