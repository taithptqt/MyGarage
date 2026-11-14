using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class SupplierPayment
{
    public long Id { get; set; }

    public long AccountsPayableId { get; set; }

    public long SupplierId { get; set; }

    public long? BranchId { get; set; }

    public long? PaymentMethodId { get; set; }

    public decimal PaidAmount { get; set; }

    public DateTime PaidAt { get; set; }

    public string? TransactionCode { get; set; }

    public string? Note { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AccountsPayable AccountsPayable { get; set; } = null!;

    public virtual ICollection<BankTransaction> BankTransactions { get; set; } = new List<BankTransaction>();

    public virtual Branch? Branch { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual ICollection<FinanceApprovalRequest> FinanceApprovalRequests { get; set; } = new List<FinanceApprovalRequest>();

    public virtual ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();

    public virtual PaymentMethod? PaymentMethod { get; set; }

    public virtual Supplier Supplier { get; set; } = null!;
}
