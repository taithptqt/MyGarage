using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class JournalEntry
{
    public long Id { get; set; }

    public string EntryNo { get; set; } = null!;

    public DateOnly EntryDate { get; set; }

    public long? PeriodId { get; set; }

    public string SourceType { get; set; } = null!;

    public long? SourceId { get; set; }

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public long? PostedBy { get; set; }

    public DateTime? PostedAt { get; set; }

    public long? ReversedEntryId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<FinanceApprovalRequest> FinanceApprovalRequests { get; set; } = new List<FinanceApprovalRequest>();

    public virtual ICollection<JournalEntry> InverseReversedEntry { get; set; } = new List<JournalEntry>();

    public virtual ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();

    public virtual AccountingPeriod? Period { get; set; }

    public virtual Staff? PostedByNavigation { get; set; }

    public virtual JournalEntry? ReversedEntry { get; set; }
}
