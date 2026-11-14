namespace MyGarage.Application.Invoices;

public sealed record InvoiceViewRow(long Id, long? ServiceOrderId, long? RetailOrderId, long? VehicleId,
    string? Phone, string? Address, string? InvoiceUrl, string? CompanyName, string? CompanyAddress,
    string? ReceiveEmail, string? TaxCode, decimal? SubTotalAmount, decimal? TaxAmount, decimal? TotalAmount,
    string? PayerType, long? PayerId, decimal? PaidAmount, string? PaymentStatus, long? CreditApprovedByStaffId,
    string? CreditNote, DateTime? IssuedAt, DateTime? CreatedAt, long? CreatedBy, DateTime? UpdatedAt, long? UpdatedBy);

public sealed record InvoiceLineRow(long Id, long InvoiceId, long? ReferenceServiceOrderDetailId,
    string? ItemType, long? ProductId, long? StandardServiceId, string? ItemName, decimal? Quantity,
    decimal? CostPriceAtTime, decimal? UnitPriceAtTime, string? Unit, decimal? SubTotal,
    decimal? DiscountAllocatedAmount, decimal? TaxRate, decimal? TaxAmount, decimal? FinalAmount, string? Note);

public sealed record InvoicePaymentRow(long Id, long? ShiftClosingId, long? CashierId, long? ServiceOrderId,
    long? InvoiceId, long? RetailOrderId, string? PayerType, long? PayerId, long MethodId, decimal? Amount,
    string? TransactionCode, string? Type, string? Status, long? ReversedPaymentId,
    DateTime? CreatedAt, DateTime? UpdatedAt, long? UpdatedBy);

public sealed record InvoiceViewDetail(InvoiceViewRow Invoice, IReadOnlyList<InvoiceLineRow> Details,
    int DetailsTotal, IReadOnlyList<InvoicePaymentRow> Payments, int PaymentsTotal,
    int PageSize, int DetailsPage, int PaymentsPage);

public interface IInvoiceViewDetailReader
{
    Task<InvoiceViewDetail?> ReadAsync(long invoiceId, int detailsPage, int paymentsPage, int pageSize, CancellationToken cancellationToken);
}

public sealed class InvoiceViewDetailQuery(IInvoiceViewDetailReader reader)
{
    public Task<InvoiceViewDetail?> ExecuteAsync(long invoiceId, int? detailsPage, int? paymentsPage, int? pageSize, CancellationToken cancellationToken)
    {
        var size = pageSize.GetValueOrDefault(50);
        if (size <= 0) size = 50;
        return reader.ReadAsync(invoiceId, Math.Max(1, detailsPage ?? 1), Math.Max(1, paymentsPage ?? 1), Math.Clamp(size, 1, 100), cancellationToken);
    }
}
