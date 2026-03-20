using System;
namespace MyGarage.DTOs.Requests
{
    public class AiFraudMonitorUpdateDto
    {
        public string? EntityType { get; set; }
        public long EntityId { get; set; }
        public string? RiskLevel { get; set; }
        public string? Reason { get; set; }
        public string? Status { get; set; }
    }
}
