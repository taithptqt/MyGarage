using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class TaxFiling
{
    public long Id { get; set; }

    public string TaxType { get; set; } = null!;

    public string PeriodCode { get; set; } = null!;

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public string Status { get; set; } = null!;

    public long? PreparedBy { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public string? AttachmentUrl { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Staff? PreparedByNavigation { get; set; }
}
