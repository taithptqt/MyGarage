using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class EmployeeDependent
{
    public long Id { get; set; }

    public long StaffId { get; set; }

    public string FullName { get; set; } = null!;

    public string Relationship { get; set; } = null!;

    public DateOnly? Dob { get; set; }

    public DateOnly? TaxRegistrationDate { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Staff Staff { get; set; } = null!;
}
