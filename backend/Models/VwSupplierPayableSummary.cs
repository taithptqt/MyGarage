using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class VwSupplierPayableSummary
{
    public long Id { get; set; }

    public long SupplierId { get; set; }

    public string SupplierName { get; set; } = null!;

    public long? BranchId { get; set; }

    public string SupplierInvoiceNo { get; set; } = null!;

    public DateOnly SupplierInvoiceDate { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal OriginalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal OutstandingAmount { get; set; }

    public string Status { get; set; } = null!;

    public long? PurchaseOrderId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
