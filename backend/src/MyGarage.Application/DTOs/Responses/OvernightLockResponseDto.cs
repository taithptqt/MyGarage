using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class OvernightLockResponseDto
    {
        public long BranchId { get; set; }
        public DateOnly BusinessDate { get; set; }
        public int LockedSlotCount { get; set; }
        public List<OvernightLockedSlotDto> LockedSlots { get; set; } = new();
    }

    public class OvernightLockedSlotDto
    {
        public long SourceWorkSlotId { get; set; }
        public long LockedWorkSlotId { get; set; }
        public long BayId { get; set; }
        public DateTime LockedFrom { get; set; }
        public DateTime LockedTo { get; set; }
        public long? VehicleId { get; set; }
    }
}
