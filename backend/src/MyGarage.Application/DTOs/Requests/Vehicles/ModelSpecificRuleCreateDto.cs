namespace MyGarage.DTOs.Requests
{
    public class ModelSpecificRuleCreateDto
    {
        public long ModelId { get; set; }
        public long ProductId { get; set; }
        public decimal StandardCapacity { get; set; }
        public decimal? Tolerance { get; set; }
    }
}
