using System;
namespace MyGarage.DTOs.Responses
{
    public class AiFraudMonitorResponseDto
    {
        public long Id { get; set; }
        public string? EntityType { get; set; }
        public long EntityId { get; set; }
        public string? RiskLevel { get; set; }
        public string? Reason { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
