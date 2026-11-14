using System;

namespace MyGarage.DTOs.Responses
{
    public class DispatchCycleResponseDto
    {
        public long BranchId { get; set; }
        public DateTime ExecutedAt { get; set; }
        public int AutoNoShowCount { get; set; }
        public int DelayNotificationCount { get; set; }
        public BranchBookingHealthResponseDto BookingHealth { get; set; } = new();
    }
}
