namespace MyGarage.DTOs.Requests;

public sealed class ServiceAdvisorCommissionRuleUpsertRequestDto
{
    public long BranchId { get; set; }
    public long StaffTierId { get; set; }
    public long? TargetId { get; set; }
    public string? CommissionType { get; set; }
    public decimal? CommissionValue { get; set; }
    public bool IsActive { get; set; } = true;
}

