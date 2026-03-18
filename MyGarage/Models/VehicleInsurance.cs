using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class VehicleInsurance
{
    public long Id { get; set; }

    public long VehicleId { get; set; }

    public long InsuranceCompanyId { get; set; }

    public string PolicyNumber { get; set; } = null!;

    public DateOnly ExpiryDate { get; set; }

    public virtual InsuranceCompany InsuranceCompany { get; set; } = null!;

    public virtual Vehicle Vehicle { get; set; } = null!;
}
