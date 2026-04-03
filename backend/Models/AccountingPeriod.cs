using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class AccountingPeriod
{
    public long Id { get; set; }

    public long? BranchId { get; set; }

    public string PeriodCode { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Status { get; set; } = null!;

    public long? ClosedBy { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual Staff? ClosedByNavigation { get; set; }

    public virtual ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
}
