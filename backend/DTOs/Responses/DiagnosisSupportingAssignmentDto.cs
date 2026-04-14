namespace MyGarage.DTOs.Responses
{
    public class DiagnosisSupportingAssignmentDto
    {
        public long TeamId { get; set; }
        public string? TeamName { get; set; }
        public long? TechnicianId { get; set; }
        public string? TechnicianName { get; set; }
        public long? AssistantTechnicianId { get; set; }
        public string? AssistantTechnicianName { get; set; }
        public int? RequestedTechnicianCount { get; set; }
    }
}
