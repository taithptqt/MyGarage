using System;

namespace MyGarage.DTOs.Responses;

public sealed class BranchQuotationApprovalPolicyResponseDto
{
    public long BranchId { get; set; }
    public decimal MaxDiscountPercentWithoutManager { get; set; }
    public decimal HighValueThreshold { get; set; }
    public bool IsActive { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
}
