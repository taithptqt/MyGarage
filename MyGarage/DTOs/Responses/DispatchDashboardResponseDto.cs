using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class DispatchDashboardResponseDto
    {
        public long BranchId { get; set; }
        public DateTime SnapshotAt { get; set; }
        public int TotalBays { get; set; }
        public int BusyBays { get; set; }
        public int IdleBays { get; set; }
        public int OverflowBays { get; set; }
        public int WaitingQueueCount { get; set; }
        public List<DispatchBayStatusDto> Bays { get; set; } = new();
    }

    public class DispatchBayStatusDto
    {
        public long BayId { get; set; }
        public string BayCode { get; set; } = string.Empty;
        public string BayName { get; set; } = string.Empty;
        public string BayType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsOverflowFromQuick { get; set; }
        public string? SlotType { get; set; }
        public long? CurrentCustomerId { get; set; }
        public long? CurrentVehicleId { get; set; }
        public DateTime? PlannedReleaseAt { get; set; }
        public DateTime? ActualReleaseAt { get; set; }
    }
}
