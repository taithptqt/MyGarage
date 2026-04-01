using System;

namespace MyGarage.Models;

public partial class SalaryCoefficient
{
    public long Id { get; set; }

    public int? YearsOfService { get; set; }

    public string CoefficientName { get; set; } = null!;

    public decimal CoefficientValue { get; set; }

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }
}
