using System;
namespace MyGarage.DTOs.Requests
{
    public class GatePassUpdateDto
    {
        public long ServiceOrderId { get; set; }
        public string? ClearanceType { get; set; }
        public string? QrCode { get; set; }
        public long IssuedBy { get; set; }
        public long? ApprovedBy { get; set; }
        public string? ResolutionNotes { get; set; }
        public string? OldPartsPackingListUrl { get; set; }
        public bool? IsOldPartsChecked { get; set; }
        public string? Status { get; set; }
        public long? ScannedBy { get; set; }
        public DateTime? SecurityCheckedAt { get; set; }
        public DateTime? PassedAt { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
    }
}
