using System;
namespace MyGarage.DTOs.Requests
{
    public class AiModelConfigUpdateDto
    {
        public string? Provider { get; set; }
        public string? ApiKey { get; set; }
        public string? ModelName { get; set; }
        public int? MaxTokens { get; set; }
        public decimal? Temperature { get; set; }
        public bool? IsActive { get; set; }
    }
}
