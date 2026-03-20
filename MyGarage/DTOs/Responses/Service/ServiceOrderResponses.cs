using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses.Service
{
    public class FlowServiceOrderResponseDto
    {
        public long Id { get; set; }
        public string Code { get; set; } = null!;
        public long CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public long VehicleId { get; set; }
        public string? VehiclePlate { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public decimal? FinalAmount { get; set; }
        
        public List<FlowDiagnosisResponseDto> Diagnoses { get; set; } = new List<FlowDiagnosisResponseDto>();
        public List<FlowServiceOrderDetailResponseDto> Details { get; set; } = new List<FlowServiceOrderDetailResponseDto>();
    }

    public class FlowDiagnosisResponseDto
    {
        public long Id { get; set; }
        public string? Symptom { get; set; }
        public string? RootCause { get; set; }
        public string? Severity { get; set; }
        public string? Status { get; set; }
        public DateTime? DiagnosedAt { get; set; }
        public string? TechnicianName { get; set; }
        
        public List<FlowRecommendationResponseDto> Recommendations { get; set; } = new List<FlowRecommendationResponseDto>();
    }

    public class FlowRecommendationResponseDto
    {
        public long Id { get; set; }
        public string ItemType { get; set; } = null!;
        public string ItemName { get; set; } = null!;
        public decimal Quantity { get; set; }
        public string? Urgency { get; set; }
        public string? TechnicianNotes { get; set; }
        public decimal? EstimatedPrice { get; set; }
    }

    public class FlowServiceOrderDetailResponseDto
    {
        public long Id { get; set; }
        public string ItemName { get; set; } = null!;
        public string ItemType { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal UnitPriceAtTime { get; set; }
        public decimal FinalAmount { get; set; }
        public string Status { get; set; } = null!;
    }
}
