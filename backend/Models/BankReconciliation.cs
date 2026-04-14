using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class BankReconciliation
{
    public long Id { get; set; }

    public long BankAccountId { get; set; }

    public DateOnly PeriodFrom { get; set; }

    public DateOnly PeriodTo { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal ClosingBalanceStatement { get; set; }

    public decimal ClosingBalanceSystem { get; set; }

    public decimal Difference { get; set; }

    public string Status { get; set; } = null!;

    public long? PreparedBy { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual BankAccount BankAccount { get; set; } = null!;

    public virtual Staff? PreparedByNavigation { get; set; }
}
