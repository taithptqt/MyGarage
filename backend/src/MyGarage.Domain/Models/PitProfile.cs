using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class PitProfile
{
    public long Id { get; set; }

    public long StaffId { get; set; }

    public string? TaxCode { get; set; }

    public string ResidentType { get; set; } = null!;

    public int DependentCount { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Staff Staff { get; set; } = null!;
}
