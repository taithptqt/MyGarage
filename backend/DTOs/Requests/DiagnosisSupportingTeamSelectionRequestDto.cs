namespace MyGarage.DTOs.Requests
{
    public class DiagnosisSupportingTeamSelectionRequestDto
    {
        public long TeamLeaderId { get; set; }
        public long TeamId { get; set; }
        public long TechnicianId { get; set; }
        public long? AssistantTechnicianId { get; set; }
        public string? Note { get; set; }
    }
}
