using System;

namespace MyGarage.DTOs.Responses
{
    public class AppointmentEditCapabilityResponseDto
    {
        public long AppointmentId { get; set; }
        public bool CanEdit { get; set; }
        public bool IsStatusLocked { get; set; }
        public bool IsWindowPassed { get; set; }
        public bool IsOverrideActive { get; set; }
        public long? OverrideRequestId { get; set; }
        public DateTime? OverrideUntilUtc { get; set; }
        public string? BlockingReason { get; set; }
    }
}
