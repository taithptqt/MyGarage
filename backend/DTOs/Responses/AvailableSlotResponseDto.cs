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
        public bool IsRoundedHour { get; set; }
        public bool IsTopPick { get; set; }
        public string Session { get; set; } = string.Empty;
        public int CompactionScore { get; set; }
        public string BookingFlow { get; set; } = "FIXED";
        public int EstimatedDurationMinutes { get; set; }
        public int ServiceMinutes { get; set; }
        public int SetupMinutes { get; set; }
        public int BufferMinutes { get; set; }
        public string? DynamicNotice { get; set; }
    }
}
