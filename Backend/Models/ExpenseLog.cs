using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ExpenseLog
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public long? ShiftClosingId { get; set; }

    public string ExpenseType { get; set; } = null!;

    public decimal Amount { get; set; }

    public string? Description { get; set; }

    public long? StaffId { get; set; }

    public string? ProofUrl { get; set; }

    public string? Status { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ShiftClosing? ShiftClosing { get; set; }

    public virtual Staff? Staff { get; set; }
}
