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

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        public long? CustomerId { get; set; }

        public long? CreatedBy { get; set; }

        public string? RequestedServiceIds { get; set; }

        public string? CustomerNotes { get; set; }
    }
}
