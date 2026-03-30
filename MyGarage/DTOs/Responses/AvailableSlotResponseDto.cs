using System;

namespace MyGarage.DTOs.Responses
{
    public class AvailableSlotResponseDto
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int MaxCapacity { get; set; }
        public int BookedCount { get; set; }
        public int RemainingCapacity { get; set; }
        public bool IsAvailable { get; set; }
    }
}
