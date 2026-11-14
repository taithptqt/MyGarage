namespace MyGarage.Application.Payments;

public sealed record InvoiceTaxSummaryItem(long Id, long? ServiceOrderId, long? RetailOrderId, decimal? TaxAmount, DateTime? IssuedAt);
public sealed record InvoiceTaxSummary(decimal TotalTax, int TaxedCount, IReadOnlyList<InvoiceTaxSummaryItem> Items);

public interface IInvoiceTaxSummaryReader
{
    Task<InvoiceTaxSummary> ReadAsync(CancellationToken cancellationToken);
}

public sealed class InvoiceTaxSummaryQuery(IInvoiceTaxSummaryReader reader)
{
    public Task<InvoiceTaxSummary> ExecuteAsync(CancellationToken cancellationToken) => reader.ReadAsync(cancellationToken);
}
