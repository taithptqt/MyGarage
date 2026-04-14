using System;

namespace MyGarage.DTOs.Requests
{
    public class DiagnosisHandoverSnapshotCreateDto
    {
        public int AdditionalRepairMinutes { get; set; }
        public string? QuoteChannel { get; set; }
        public string? QuoteSummary { get; set; }
        public string? QuoteAttachmentUrl { get; set; }
        public DateTime? T1EstimatedHandoverOverride { get; set; }
        public long? CreatedByStaffId { get; set; }
    }
}
