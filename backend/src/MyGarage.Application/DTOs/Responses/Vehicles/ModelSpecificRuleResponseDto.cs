namespace MyGarage.DTOs.Responses
{
    public class ModelSpecificRuleResponseDto
    {
        public long ModelId { get; set; }
        public long ProductId { get; set; }
        public decimal StandardCapacity { get; set; }
        public decimal? Tolerance { get; set; }
    }
}
