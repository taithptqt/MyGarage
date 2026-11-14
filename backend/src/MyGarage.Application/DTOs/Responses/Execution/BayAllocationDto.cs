using System;

namespace MyGarage.DTOs.Responses.Execution
{
    public class BayAllocationDto
    {
        public long Id { get; set; }
        public long BayId { get; set; }
        public string BayName { get; set; } = string.Empty;
        public long ServiceOrderId { get; set; }
        public string ServiceOrderCode { get; set; } = string.Empty;
        public long VehicleId { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        
        public DateTime AllocatedAt { get; set; }
        public long AllocatedBy { get; set; }
        public string AllocatedByName { get; set; } = string.Empty;
        
        public DateTime? ReleasedAt { get; set; }
        public long? ReleasedBy { get; set; }
        public string? ReleasedByName { get; set; }
        
        public int? PlannedDuration { get; set; }
        public int? ActualDuration { get; set; }
        public string? Notes { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
