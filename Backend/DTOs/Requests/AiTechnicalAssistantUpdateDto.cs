using System;
namespace MyGarage.DTOs.Requests
{
    public class AiTechnicalAssistantUpdateDto
    {
        public long MechanicId { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public long? RelatedDiagnoseId { get; set; }
    }
}
