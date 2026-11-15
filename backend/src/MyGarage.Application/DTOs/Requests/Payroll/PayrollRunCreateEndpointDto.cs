using System.Collections.Generic;

namespace MyGarage.DTOs.Requests;

public sealed class PayrollRunCreateItemDto
{
    public long StaffId { get; set; }
    public decimal? BaseSalary { get; set; }
    public decimal? CommissionAmount { get; set; }
    public decimal? OvertimeAmount { get; set; }
    public decimal? AllowanceAmount { get; set; }
    public decimal? Bonus { get; set; }
    public decimal? BonusAmount { get; set; }
    public decimal? Deductions { get; set; }
    public decimal? Penalty { get; set; }
    public decimal? PenaltyAmount { get; set; }
}

public sealed class PayrollRunCreateEndpointDto
{
    public string? Period { get; set; }
    public long? BranchId { get; set; }
    public long? CreatedBy { get; set; }
    public List<PayrollRunCreateItemDto>? Details { get; set; }
}
