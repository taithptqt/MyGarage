using System.Collections.Generic;

namespace MyGarage.DTOs.Requests
{
    public class DiagnosisManagerOverrideDto
    {
        public long ManagerId { get; set; }
        public long TechnicianId { get; set; }
        public long? AssistantTechnicianId { get; set; }
        public long? BayId { get; set; }
        public long? TeamId { get; set; }
        public IReadOnlyList<long>? SupportingTeamIds { get; set; }
        public string? Reason { get; set; }
    }
}
