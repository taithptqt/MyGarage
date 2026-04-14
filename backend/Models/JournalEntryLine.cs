using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class JournalEntryLine
{
    public long Id { get; set; }

    public long JournalEntryId { get; set; }

    public int LineNo { get; set; }

    public long AccountId { get; set; }

    public long? BranchId { get; set; }

    public long? SupplierId { get; set; }

    public long? PaymentId { get; set; }

    public long? InvoiceId { get; set; }

    public long? SupplierInvoiceId { get; set; }

    public long? SupplierPaymentId { get; set; }

    public string? ReferenceNo { get; set; }

    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CustomerId { get; set; }

    public long? StaffId { get; set; }

    public virtual ChartOfAccount Account { get; set; } = null!;

    public virtual Branch? Branch { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual JournalEntry JournalEntry { get; set; } = null!;

    public virtual Payment? Payment { get; set; }

    public virtual Staff? Staff { get; set; }

    public virtual Supplier? Supplier { get; set; }

    public virtual SupplierInvoice? SupplierInvoice { get; set; }

    public virtual SupplierPayment? SupplierPayment { get; set; }
}
