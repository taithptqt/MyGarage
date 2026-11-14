using System;
namespace MyGarage.DTOs.Requests
{
    public class DiagnosisRecommendationCreateDto
    {
        public long? DiagnosisId { get; set; }
        public long? DiagnosisAssignmentId { get; set; }
        public long? ParentRecommendationId { get; set; }
        public string? ItemType { get; set; }
        public long? StandardServiceId { get; set; }
        public long? InventoryItemId { get; set; }
        public decimal? Quantity { get; set; }
        public string? Urgency { get; set; }
        public string? EvidenceMediaUrls { get; set; }
        public string? TechnicianNotes { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
    }
}

