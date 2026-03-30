using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class BranchBookingHealthResponseDto
    {
        public long BranchId { get; set; }
        public DateTime EvaluatedAt { get; set; }
        public bool IsBookingLocked { get; set; }
        public DateTime? LockUntil { get; set; }
        public int TotalDelayMinutes { get; set; }
        public int DelayThresholdMinutes { get; set; }
        public int ParkingQueueCount { get; set; }
        public int ParkingThreshold { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<BranchSuggestionDto> SuggestedBranches { get; set; } = new();
    }

    public class BranchSuggestionDto
    {
        public long BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public double DistanceKm { get; set; }
        public double EstimatedUtilizationPct { get; set; }
        public DateTime? EarliestAvailableAt { get; set; }
    }
}
