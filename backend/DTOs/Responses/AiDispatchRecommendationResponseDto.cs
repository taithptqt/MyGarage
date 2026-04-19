namespace MyGarage.DTOs.Responses
{
    public class AiDispatchRecommendationResponseDto
    {
        public long ServiceOrderId { get; set; }
        public long RecommendedBayId { get; set; }
        public string BayCode { get; set; } = null!;
        public string BayName { get; set; } = null!;
        public string Reason { get; set; } = null!;
        public decimal EfficiencyScore { get; set; }
        public List<BayAlternativeDto> Alternatives { get; set; } = new();
    }

    public class BayAlternativeDto
    {
        public long BayId { get; set; }
        public string BayCode { get; set; } = null!;
        public string Reason { get; set; } = null!;
    }
}
