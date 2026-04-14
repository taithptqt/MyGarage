using System;
namespace MyGarage.DTOs.Responses
{
    public class BranchOperatingHourResponseDto
    {
        public long Id { get; set; }
        public long BranchId { get; set; }
        public int DayOfWeek { get; set; }
        public TimeOnly OpenTime { get; set; }
        public TimeOnly CloseTime { get; set; }
        public bool? IsDayOff { get; set; }
        public int SlotDurationMinutes { get; set; }
        public int MaxConcurrentBookings { get; set; }
        public long? UpdatedByStaffId { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
