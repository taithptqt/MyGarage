using System;
namespace MyGarage.DTOs.Responses
{
    public class AiSmartDispatchingResponseDto
    {
        public long Id { get; set; }
        public long BranchId { get; set; }
        public DateOnly Date { get; set; }
        public string? Recommendation { get; set; }
        public decimal? EfficiencyScore { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
