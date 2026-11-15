using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class CancelGroupSessionResponseDto
    {
        public long GroupSessionId { get; set; }
        public int TotalAppointments { get; set; }
        public int TotalItems
        {
            get => TotalAppointments;
            set => TotalAppointments = value;
        }
        public int CanceledCount { get; set; }
        public int SkippedStartedCount { get; set; }
        public List<long> CanceledAppointmentIds { get; set; } = new();
        public List<long> SkippedStartedAppointmentIds { get; set; } = new();
        public List<string> ConfirmedVehiclePlates { get; set; } = new();
        public List<string> SkippedStartedVehiclePlates { get; set; } = new();
        public string GroupStatus { get; set; } = string.Empty;
    }
}
