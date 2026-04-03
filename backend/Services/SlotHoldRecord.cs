using System;

namespace MyGarage.Services
{
    public sealed class SlotHoldRecord
    {
        public string Token { get; init; } = string.Empty;
        public long BranchId { get; init; }
        public DateTime StartTime { get; init; }
        public DateTime EndTime { get; init; }
        public string BookingFlow { get; init; } = "FIXED";
        public string? RequestedServiceIds { get; init; }
        public int VehicleCount { get; init; } = 1;
        public DateTime CreatedAt { get; init; }
        public DateTime ExpiresAt { get; init; }
    }
}

