using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ShiftClosing
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public long WorkShiftId { get; set; }

    public long CashierId { get; set; }

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal? TotalCashIn { get; set; }

    public decimal? TotalCashOut { get; set; }

    public decimal? ExpectedBalance { get; set; }

    public decimal? ActualBalance { get; set; }

    public decimal? Discrepancy { get; set; }

    public string? Status { get; set; }

    public string? Note { get; set; }

    public long? ManagerId { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual Staff Cashier { get; set; } = null!;

    public virtual ICollection<ExpenseLog> ExpenseLogs { get; set; } = new List<ExpenseLog>();

    public virtual Staff? Manager { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual Shift WorkShift { get; set; } = null!;
}
