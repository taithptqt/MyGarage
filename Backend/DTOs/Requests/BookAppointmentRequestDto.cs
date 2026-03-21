using System;
using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests
{
    public class BookAppointmentRequestDto
    {
        [Required]
        public long BranchId { get; set; }

        public long? VehicleId { get; set; }

        public long? VehicleModelId { get; set; }

        public string? LicensePlate { get; set; }

        public string? Color { get; set; }

        [Required]
        public DateOnly ScheduledDate { get; set; }

        [Required]
        public TimeOnly StartTime { get; set; }

        [Required]
        public TimeOnly EndTime { get; set; }

        public string? RequestedServiceIds { get; set; }

        public string? CustomerNotes { get; set; }
    }
}
