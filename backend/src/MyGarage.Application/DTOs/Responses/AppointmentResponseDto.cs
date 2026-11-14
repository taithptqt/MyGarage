using System;
namespace MyGarage.DTOs.Responses
{
    public class AppointmentResponseDto
    {
        public long Id { get; set; }
        public long BranchId { get; set; }
        public long CustomerId { get; set; }
        public long? VehicleId { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? RequestedServiceIds { get; set; }
        public string? CustomerNotes { get; set; }
        public bool? IsReminderSent { get; set; }
        public string? Status { get; set; }
        public string? CancelReason { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
