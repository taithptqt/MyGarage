using System;

namespace MyGarage.DTOs.Requests
{
    public class SlotHoldRequestDto
    {
        public long BranchId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? BookingFlow { get; set; }
        public string? RequestedServiceIds { get; set; }
        public string? RequestedPackageIds { get; set; }
        public int VehicleCount { get; set; } = 1;
    }
}

