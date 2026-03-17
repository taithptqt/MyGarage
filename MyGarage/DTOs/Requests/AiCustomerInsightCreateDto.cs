using System;
namespace MyGarage.DTOs.Requests
{
    public class AiCustomerInsightCreateDto
    {
        public long CustomerId { get; set; }
        public decimal? ChurnRisk { get; set; }
        public decimal? BuyingPropensity { get; set; }
        public string? Segment { get; set; }
    }
}
