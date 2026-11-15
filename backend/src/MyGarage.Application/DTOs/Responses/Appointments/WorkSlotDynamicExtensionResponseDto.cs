using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class WorkSlotDynamicExtensionResponseDto
    {
        public long WorkSlotId { get; set; }
        public string Action { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; }
        public DateTime PreviousEndTime { get; set; }
        public DateTime NewEndTime { get; set; }
        public bool IsSuccessful { get; set; }
        public bool HasCollision { get; set; }
        public bool RequiresManualDispatch { get; set; }
        public bool BorrowedDvnBay { get; set; }
        public long? BorrowedBayId { get; set; }
        public string? BorrowedBayCode { get; set; }
        public bool OvernightLockCreated { get; set; }
        public long? OvernightLockSlotId { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<DynamicDispatchSuggestionDto> Suggestions { get; set; } = new();
    }

    public class DynamicDispatchSuggestionDto
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public long? BayId { get; set; }
        public string? BayCode { get; set; }
        public DateTime? AvailableFrom { get; set; }
        public DateTime? AvailableTo { get; set; }
        public long? RelatedWorkSlotId { get; set; }
    }
}
