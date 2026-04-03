using System;
namespace MyGarage.DTOs.Requests
{
    public class DiagnosisCreateDto
    {
        public long ServiceOrderId { get; set; }
        public long TechnicianId { get; set; }
        public string? Symptom { get; set; }
        public string? RootCause { get; set; }
        public string? Severity { get; set; }
        public string? Status { get; set; }
        public DateTime? DiagnosedAt { get; set; }
        public long? ApprovedByForemanId { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
