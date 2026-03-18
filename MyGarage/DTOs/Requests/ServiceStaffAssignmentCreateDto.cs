using System;
namespace MyGarage.DTOs.Requests
{
    public class ServiceStaffAssignmentCreateDto
    {
        public long ServiceOrderDetailId { get; set; }
        public long? AssignedBy { get; set; }
        public long TechnicianId { get; set; }
        public string? RoleInTask { get; set; }
        public decimal? SplitRatio { get; set; }
        public decimal? EstimatedCommission { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public string? PauseLog { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public string? Status { get; set; }
        public bool? IsRework { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
