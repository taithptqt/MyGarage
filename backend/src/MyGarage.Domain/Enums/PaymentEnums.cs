namespace MyGarage.Domain.Enums;

public enum PaymentStatusCode
{
    PENDING,
    PROCESSING,
    COMPLETED,
    FAILED,
    CANCELED
}

public enum InvoicePaymentStatusCode
{
    UNPAID,
    PARTIAL,
    PAID
}

public enum PaymentDirectionCode
{
    INFLOW,
    OUTFLOW
}

public enum JournalEntryStatusCode
{
    DRAFT,
    POSTED,
    REVERSED
}

public enum FinanceApprovalStatusCode
{
    PENDING,
    APPROVED,
    REJECTED,
    CANCELED
}
