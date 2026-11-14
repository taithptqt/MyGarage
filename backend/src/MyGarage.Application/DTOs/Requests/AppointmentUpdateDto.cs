using System;
using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests
{
    public class AppointmentUpdateDto
    {
        public long BranchId { get; set; }
        public long CustomerId { get; set; }
        public long? VehicleId { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? RequestedServiceIds { get; set; }

        [Display(Name = "Ghi chú")]
        public string? CustomerNotes { get; set; }

        public bool? IsReminderSent { get; set; }
        public string? Status { get; set; }
        public string? CancelReason { get; set; }
    }
}
