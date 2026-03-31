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

        public string? Vin { get; set; }

        public string? Color { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public DateTime? CheckInWindowStart { get; set; }

        public DateTime? CheckInWindowEnd { get; set; }

        public string? BookingFlow { get; set; }

        public long? CustomerId { get; set; }

        public string? CustomerFullName { get; set; }

        public string? CustomerPhone { get; set; }

        public long? CreatedBy { get; set; }

        public string? RequestedServiceIds { get; set; }

        public string? CustomerNotes { get; set; }
    }
}
