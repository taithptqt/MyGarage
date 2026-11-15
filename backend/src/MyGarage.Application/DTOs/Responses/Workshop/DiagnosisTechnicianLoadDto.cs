namespace MyGarage.DTOs.Responses
{
    public class DiagnosisTechnicianLoadDto
    {
        public long TechnicianId { get; set; }
        public string? TechnicianName { get; set; }
        public int ActiveDiagnosisCount { get; set; }
        public int? SkillLevel { get; set; }
    }
}
