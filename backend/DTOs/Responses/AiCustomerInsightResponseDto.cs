using System;
namespace MyGarage.DTOs.Responses
{
    public class AiCustomerInsightResponseDto
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public decimal? ChurnRisk { get; set; }
        public decimal? BuyingPropensity { get; set; }
        public string? Segment { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
