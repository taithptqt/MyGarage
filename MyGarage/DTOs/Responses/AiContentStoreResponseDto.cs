using System;
namespace MyGarage.DTOs.Responses
{
    public class AiContentStoreResponseDto
    {
        public long Id { get; set; }
        public string? ContextType { get; set; }
        public string? InputData { get; set; }
        public string? GeneratedContent { get; set; }
        public long? StaffId { get; set; }
        public DateTime? UsedAt { get; set; }
    }
}
