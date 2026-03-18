using System;
namespace MyGarage.DTOs.Requests
{
    public class BranchOperatingHourCreateDto
    {
        public long BranchId { get; set; }
        public int DayOfWeek { get; set; }
        public TimeOnly OpenTime { get; set; }
        public TimeOnly CloseTime { get; set; }
        public bool? IsDayOff { get; set; }
        public int SlotDurationMinutes { get; set; }
        public int MaxConcurrentBookings { get; set; }
        public long? UpdatedByStaffId { get; set; }
    }
}
