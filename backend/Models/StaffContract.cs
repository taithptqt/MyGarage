using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class StaffContract
{
    public long Id { get; set; }

    public long StaffId { get; set; }

    public string ContractType { get; set; } = null!;

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public decimal BaseSalary { get; set; }

    public decimal InsuranceSalaryBase { get; set; }

    public decimal AllowanceAmount { get; set; }

    public string PayFrequency { get; set; } = null!;

    public string? BankAccountName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BankName { get; set; }

    public string? TaxCode { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Staff Staff { get; set; } = null!;
}
