namespace MyGarage.Application.Invoices;

public sealed record InvoiceAuthInfo(long Id, long? PayerId, string? PayerType, long? ServiceOrderId, long? RetailOrderId);

public interface IInvoiceStore :
    IInvoiceApplicationStore,
    IInvoiceAuthorizationStore,
    IInvoiceBackfillStore,
    IInvoiceOrderContextStore,
    IInvoicePersistenceStore
{
}
