using System;
namespace MyGarage.DTOs.Responses
{
    public class AiModelConfigResponseDto
    {
        public long Id { get; set; }
        public string? Provider { get; set; }
        public bool HasApiKey { get; set; }
        public string? ModelName { get; set; }
        public int? MaxTokens { get; set; }
        public decimal? Temperature { get; set; }
        public bool? IsActive { get; set; }
    }
}
