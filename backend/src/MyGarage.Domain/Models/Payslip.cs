using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Payslip
{
    public long Id { get; set; }

    public long PayrollItemId { get; set; }

    public string SlipNo { get; set; } = null!;

    public DateTime IssuedAt { get; set; }

    public long? IssuedBy { get; set; }

    public string? FileUrl { get; set; }

    public DateTime? ViewedAt { get; set; }

    public virtual Staff? IssuedByNavigation { get; set; }

    public virtual PayrollItem PayrollItem { get; set; } = null!;
}
