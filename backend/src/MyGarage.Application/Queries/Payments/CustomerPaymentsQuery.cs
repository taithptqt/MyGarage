namespace MyGarage.Application.Payments;

public sealed record CustomerPaymentRow(
    long Id, long? ShiftClosingId, long? CashierId, long? ServiceOrderId, long? InvoiceId,
    long? RetailOrderId, string? PayerType, long? PayerId, long MethodId, decimal? Amount,
    string? TransactionCode, string? Type, string? Status, long? ReversedPaymentId,
    DateTime? CreatedAt, DateTime? UpdatedAt, long? UpdatedBy);

public sealed record CustomerPaymentsPage(IReadOnlyList<CustomerPaymentRow> Items, int Total, int Page, int PageSize);

public interface ICustomerPaymentsReader
{
    Task<(IReadOnlyList<CustomerPaymentRow> Items, int Total)> ReadAsync(
        long customerId, int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class CustomerPaymentsQuery(ICustomerPaymentsReader reader)
{
    public async Task<CustomerPaymentsPage> ExecuteAsync(long customerId, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, page ?? 1);
        var normalizedSize = pageSize.GetValueOrDefault(50);
        if (normalizedSize <= 0) normalizedSize = 50;
        normalizedSize = Math.Clamp(normalizedSize, 1, 100);
        var result = await reader.ReadAsync(customerId, normalizedPage, normalizedSize, cancellationToken);
        return new CustomerPaymentsPage(result.Items, result.Total, normalizedPage, normalizedSize);
    }
}
