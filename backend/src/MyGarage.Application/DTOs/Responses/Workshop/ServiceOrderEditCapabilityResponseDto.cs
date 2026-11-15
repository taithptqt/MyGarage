using System;

namespace MyGarage.DTOs.Responses
{
    public class ServiceOrderEditCapabilityResponseDto
    {
        public long ServiceOrderId { get; set; }
        public bool CanEditWorkshop { get; set; }
        public bool IsWindowPassed { get; set; }
        public bool IsQuoteLocked { get; set; }
        public bool IsAssignmentLocked { get; set; }
        public bool IsPartsLocked { get; set; }
        public bool IsBillingLocked { get; set; }
        public bool IsClosedLocked { get; set; }
        public bool IsOverrideActive { get; set; }
        public bool CanOverrideWorkshop { get; set; }
        public bool CanOverrideQuote { get; set; }
        public bool CanOverrideAssignment { get; set; }
        public bool CanOverrideParts { get; set; }
        public bool CanOverrideBilling { get; set; }
        public bool CanOverrideClosed { get; set; }
        public long? OverrideRequestId { get; set; }
        public DateTime? OverrideUntilUtc { get; set; }
        public string? BlockingReason { get; set; }
    }
}
