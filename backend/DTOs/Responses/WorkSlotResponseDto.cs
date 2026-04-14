using System;
namespace MyGarage.DTOs.Responses
{
    public class WorkSlotResponseDto
    {
        public long Id { get; set; }
        public long? ParentSlotId { get; set; }
        public long BranchId { get; set; }
        public long? BayId { get; set; }
        public long? TechnicianId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? SlotType { get; set; }
        public long? CustomerId { get; set; }
        public long? VehicleId { get; set; }
        public long? AppointmentId { get; set; }
        public long? ServiceOrderId { get; set; }
        public bool IsAdditional { get; set; }
        public int SequenceOrder { get; set; }
        public long? ServiceStaffAssignmentId { get; set; }
        public string? Status { get; set; }
        public DateTime? LockedUntil { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualReleaseTime { get; set; }
        public string? Notes { get; set; }
    }
}
