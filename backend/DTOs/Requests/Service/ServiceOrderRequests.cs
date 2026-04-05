using System;

namespace MyGarage.DTOs.Requests.Service
{
    public class CreateServiceOrderRequestDto
    {
        public long CustomerId { get; set; }
        public long VehicleId { get; set; }
        public long ExecuteBranchId { get; set; }
        public long? AppointmentId { get; set; }
        public long? GroupSessionId { get; set; }
        public string? CustomerNotes { get; set; }
        public string? OrderCategory { get; set; } // e.g., Maintenance, Repair, Warranty
    }

    public class CreateDiagnosisRequestDto
    {
        public long ServiceOrderId { get; set; }
        public long TechnicianId { get; set; }
        public string? Symptom { get; set; }
        public string? RootCause { get; set; }
        public string? Severity { get; set; } // e.g., Low, Medium, High, Critical
    }

    public class CreateRecommendationRequestDto
    {
        public string ItemType { get; set; } = null!; // "Service" or "Part"
        public long? StandardServiceId { get; set; }
        public long? InventoryItemId { get; set; }
        public decimal Quantity { get; set; }
        public string? Urgency { get; set; } // e.g., Immediate, Recommended, Optional
        public string? TechnicianNotes { get; set; }
    }

    public class ApproveRecommendationsRequestDto
    {
        public List<long> RecommendationIds { get; set; } = new List<long>();
    }

    public class UpdateOrderStatusRequestDto
    {
        public string Status { get; set; } = null!;
    }
}
