namespace MyGarage.Application.Payments;

public sealed record InvoiceListItem(
    long Id, long? ServiceOrderId, long? RetailOrderId, string? InvoiceUrl,
    string? CompanyName, string? CompanyAddress, string? ReceiveEmail, string? TaxCode,
    decimal? TaxAmount, decimal? TotalAmount, decimal? PaidAmount, string? PaymentStatus,
    string? CreditNote, DateTime? IssuedAt);

public sealed record InvoiceListSummary(int TotalInvoice, int IssuedCount, int PaidCount, int UnknownStatusCount);
public sealed record InvoiceListPage(IReadOnlyList<InvoiceListItem> Items, int Total, int Page, int PageSize, InvoiceListSummary Summary);
public sealed record InvoiceListCriteria(string Search, string Status, string Source, int Page, int PageSize);

public interface IInvoiceListPageReader
{
    Task<InvoiceListPage> ReadAsync(InvoiceListCriteria criteria, CancellationToken cancellationToken);
}

public sealed class InvoiceListPageQuery(IInvoiceListPageReader reader)
{
    public Task<InvoiceListPage> ExecuteAsync(string? search, string? status, string? source, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var size = pageSize.GetValueOrDefault(50);
        if (size <= 0) size = 50;
        return reader.ReadAsync(new InvoiceListCriteria((search ?? "").Trim().ToLowerInvariant(), status ?? "ALL", source ?? "ALL",
            Math.Max(1, page ?? 1), Math.Clamp(size, 1, 100)), cancellationToken);
    }
}
