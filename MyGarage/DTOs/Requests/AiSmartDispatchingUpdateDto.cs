using System;
namespace MyGarage.DTOs.Requests
{
    public class AiSmartDispatchingUpdateDto
    {
        public long BranchId { get; set; }
        public DateOnly Date { get; set; }
        public string? Recommendation { get; set; }
        public decimal? EfficiencyScore { get; set; }
        public string? Status { get; set; }
    }
}
