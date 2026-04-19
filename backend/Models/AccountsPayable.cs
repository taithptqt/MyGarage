using System;
using System.Collections.Generic;
using MyGarage.Interfaces;

namespace MyGarage.Models;

public partial class AccountsPayable : IAuditableEntity
{
    public long Id { get; set; }

    public long? SupplierInvoiceId { get; set; }

    public long SupplierId { get; set; }

    public long? BranchId { get; set; }

    public string SupplierInvoiceNo { get; set; } = null!;

    public DateOnly SupplierInvoiceDate { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal OriginalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal OutstandingAmount { get; set; }

    public string Status { get; set; } = null!;

    public long? PurchaseOrderId { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }
    
    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
    
    public long? UpdatedBy { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual PurchaseOrder? PurchaseOrder { get; set; }

    public virtual Supplier Supplier { get; set; } = null!;

    public virtual SupplierInvoice? SupplierInvoice { get; set; }

    public virtual ICollection<SupplierPayment> SupplierPayments { get; set; } = new List<SupplierPayment>();
}
