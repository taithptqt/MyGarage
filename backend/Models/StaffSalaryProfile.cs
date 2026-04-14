using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class StaffSalaryProfile
{
    public long Id { get; set; }

    public long StaffId { get; set; }

    public long? BranchId { get; set; }

    public decimal BaseSalary { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual Staff Staff { get; set; } = null!;

    public virtual Staff? UpdatedByNavigation { get; set; }
}
