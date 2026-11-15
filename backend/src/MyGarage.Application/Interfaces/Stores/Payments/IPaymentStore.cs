namespace MyGarage.Application.Payments;

public sealed record PaymentAuthInfo(long Id, long? PayerId, string? PayerType, long? ServiceOrderId, long? RetailOrderId, long? InvoiceId);
public sealed record CustomerEmailInfo(string? Email, string? FullName);
public sealed record InvoiceEmailInfo(string? PayerType, long? PayerId, string? ReceiveEmail);
public sealed record OrderBuyerInfo(string? BuyerEmail, string? BuyerName);

public interface IPaymentStore :
    IPaymentCoreStore,
    IPaymentAuthorizationStore,
    IPaymentSettlementStore,
    IPaymentNotificationQueryStore
{
}
