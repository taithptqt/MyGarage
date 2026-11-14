using System.Collections.Generic;

namespace MyGarage.DTOs.Requests
{
    public sealed class ServiceStaffAssignmentMultiCreateDto
    {
        public long ServiceOrderDetailId { get; set; }
        public long? AssignedBy { get; set; }
        public List<ServiceStaffAssignmentMemberDto> Assignments { get; set; } = new();
    }

    public sealed class ServiceStaffAssignmentMemberDto
    {
        public long TechnicianId { get; set; }
        public string? RoleInTask { get; set; }
        public decimal? SplitRatio { get; set; }
        public decimal? EstimatedCommission { get; set; }
        public string? Status { get; set; }
        public bool RequiresInsuranceProof { get; set; }
        public string? ProofImageUrl { get; set; }
        public bool? IsRework { get; set; }
    }
}
