using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class FinanceApprovalRequest
{
    public long Id { get; set; }

    public string RequestType { get; set; } = null!;

    public long? InvoiceId { get; set; }

    public long? PaymentId { get; set; }

    public long? SupplierInvoiceId { get; set; }

    public long? SupplierPaymentId { get; set; }

    public long? JournalEntryId { get; set; }

    public long? PayrollRunId { get; set; }

    public long? TaxFilingId { get; set; }

    public long RequestedBy { get; set; }

    public long? ApproverId { get; set; }

    public string Status { get; set; } = null!;

    public string RiskLevel { get; set; } = null!;

    public decimal? Amount { get; set; }

    public string Reason { get; set; } = null!;

    public string? RejectReason { get; set; }

    public string? RequestPayload { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Staff? Approver { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual JournalEntry? JournalEntry { get; set; }

    public virtual Payment? Payment { get; set; }

    public virtual Staff RequestedByNavigation { get; set; } = null!;

    public virtual SupplierInvoice? SupplierInvoice { get; set; }

    public virtual SupplierPayment? SupplierPayment { get; set; }
}
