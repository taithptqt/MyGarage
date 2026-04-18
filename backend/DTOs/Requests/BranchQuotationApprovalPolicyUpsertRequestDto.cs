namespace MyGarage.DTOs.Requests;

public sealed class BranchQuotationApprovalPolicyUpsertRequestDto
{
    public long ActorStaffId { get; set; }
    public decimal? MaxDiscountPercentWithoutManager { get; set; }
    public decimal? HighValueThreshold { get; set; }
    public bool IsActive { get; set; } = true;
}
