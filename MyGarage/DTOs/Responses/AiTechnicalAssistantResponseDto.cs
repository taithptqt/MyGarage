using System;
namespace MyGarage.DTOs.Responses
{
    public class AiTechnicalAssistantResponseDto
    {
        public long Id { get; set; }
        public long MechanicId { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public long? RelatedDiagnoseId { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
