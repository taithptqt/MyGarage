using System;

namespace MyGarage.DTOs.Responses
{
    public class ReceptionAppointmentSaveResponseDto
    {
        public long? GroupSessionId { get; set; }
        public int CreatedCount { get; set; }
        public bool UpdateMode { get; set; }
        public long? PrimaryAppointmentId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? VehicleLicensePlate { get; set; }
        public DateTime? StartTime { get; set; }
    }
}
