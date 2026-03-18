using System;
namespace MyGarage.DTOs.Requests
{
    public class AiKnowledgeBaseUpdateDto
    {
        public string? Topic { get; set; }
        public string? Content { get; set; }
        public string? SourceUrl { get; set; }
        public bool? IsDynamic { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
}
