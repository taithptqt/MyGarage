using System;
namespace MyGarage.DTOs.Responses
{
    public class AiKnowledgeBaseResponseDto
    {
        public long Id { get; set; }
        public string? Topic { get; set; }
        public string? Content { get; set; }
        public string? SourceUrl { get; set; }
        public bool? IsDynamic { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
}
