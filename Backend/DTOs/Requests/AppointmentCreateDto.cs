using System;
namespace MyGarage.DTOs.Requests
{
    public class AppointmentCreateDto
    {
        public long BranchId { get; set; }
        public long CustomerId { get; set; }
        public long? VehicleId { get; set; }
        public long? CreatedBy { get; set; }
        public DateOnly ScheduledDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string? RequestedServiceIds { get; set; }
        public string? CustomerNotes { get; set; }
        public bool? IsReminderSent { get; set; }
        public string? Status { get; set; }
        public string? CancelReason { get; set; }
    }
}
