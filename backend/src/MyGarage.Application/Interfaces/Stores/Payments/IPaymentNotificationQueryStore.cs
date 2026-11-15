using MyGarage.Models;

namespace MyGarage.Application.Payments;

public interface IPaymentNotificationQueryStore
{
    Task<CustomerEmailInfo?> GetCustomerEmailInfoAsync(long customerId, CancellationToken ct = default);
    Task<CustomerEmailInfo?> GetServiceOrderCustomerEmailInfoAsync(long serviceOrderId, CancellationToken ct = default);
    Task<CustomerEmailInfo?> GetRetailOrderRecipientInfoAsync(long retailOrderId, CancellationToken ct = default);
    OrderBuyerInfo? GetOrderBuyerInfoByPaymentLinkId(string paymentLinkId);
    Task<InvoiceEmailInfo?> GetInvoiceEmailInfoAsync(long invoiceId, CancellationToken ct = default);
    Task<long?> GetRetailOrderCustomerIdAsync(long retailOrderId, CancellationToken ct = default);
    Task<RetailOrder?> GetRetailOrderWithItemsForEmailAsync(long retailOrderId, CancellationToken ct = default);
}
