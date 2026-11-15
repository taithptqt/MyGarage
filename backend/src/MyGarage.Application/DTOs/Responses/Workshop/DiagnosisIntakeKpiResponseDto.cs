using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class DiagnosisIntakeKpiResponseDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public long? BranchId { get; set; }
        public int TotalIntakeOrders { get; set; }
        public int TotalApprovedAdditionalRepairOrders { get; set; }
        public IReadOnlyList<DiagnosisIntakePackageKpiItemDto> PackageConversions { get; set; } = Array.Empty<DiagnosisIntakePackageKpiItemDto>();
        public IReadOnlyList<DiagnosisIntakeTeamKpiItemDto> TeamConversions { get; set; } = Array.Empty<DiagnosisIntakeTeamKpiItemDto>();
    }

    public class DiagnosisIntakePackageKpiItemDto
    {
        public string PackageCode { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public int TotalOrders { get; set; }
        public int ApprovedAdditionalRepairOrders { get; set; }
        public int RejectedAdditionalRepairOrders { get; set; }
        public int PendingDecisionOrders { get; set; }
        public decimal ApprovalRatePercent { get; set; }
    }

    public class DiagnosisIntakeTeamKpiItemDto
    {
        public long? TeamId { get; set; }
        public string TeamName { get; set; } = "Chưa phân tổ";
        public int TotalOrders { get; set; }
        public int ApprovedAdditionalRepairOrders { get; set; }
        public int RejectedAdditionalRepairOrders { get; set; }
        public int PendingDecisionOrders { get; set; }
        public decimal ApprovalRatePercent { get; set; }
    }
}
