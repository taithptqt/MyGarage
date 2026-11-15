namespace MyGarage.DTOs.Requests
{
    public class DiagnosisSupportingTeamAssignmentDto
    {
        public long TeamId { get; set; }
        public long TechnicianId { get; set; }
        public long? AssistantTechnicianId { get; set; }
        public int? RequestedTechnicianCount { get; set; }
    }
}
