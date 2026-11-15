using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class DiagnosisDispatchSuggestionDto
    {
        public long ServiceOrderId { get; set; }
        public long TeamId { get; set; }
        public string? TeamName { get; set; }
        public long? SuggestedLeadTechnicianId { get; set; }
        public string? SuggestedLeadTechnicianName { get; set; }
        public long? SuggestedAssistantTechnicianId { get; set; }
        public string? SuggestedAssistantTechnicianName { get; set; }
        public long? SuggestedBayId { get; set; }
        public string? SuggestedBayCode { get; set; }
        public IReadOnlyList<DiagnosisTechnicianLoadDto> TechnicianLoads { get; set; } = new List<DiagnosisTechnicianLoadDto>();
    }
}
