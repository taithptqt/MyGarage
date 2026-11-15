namespace MyGarage.DTOs.Requests
{
    public class ModelSpecificRuleUpdateDto
    {
        public long? ModelId { get; set; }
        public long? ProductId { get; set; }
        public decimal StandardCapacity { get; set; }
        public decimal? Tolerance { get; set; }
    }
}
