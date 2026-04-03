using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class SupplierInvoice
{
    public long Id { get; set; }

    public long SupplierId { get; set; }

    public long? PurchaseOrderId { get; set; }

    public string InvoiceNo { get; set; } = null!;

    public DateOnly InvoiceDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public decimal SubTotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = null!;

    public string? AttachmentUrl { get; set; }

    public string? Note { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual AccountsPayable? AccountsPayable { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual ICollection<FinanceApprovalRequest> FinanceApprovalRequests { get; set; } = new List<FinanceApprovalRequest>();

    public virtual ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();

    public virtual PurchaseOrder? PurchaseOrder { get; set; }

    public virtual Supplier Supplier { get; set; } = null!;

    public virtual ICollection<SupplierInvoiceDetail> SupplierInvoiceDetails { get; set; } = new List<SupplierInvoiceDetail>();
}
