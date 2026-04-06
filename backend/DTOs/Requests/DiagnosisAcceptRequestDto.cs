namespace MyGarage.DTOs.Requests
{
    public class DiagnosisAcceptRequestDto
    {
        public long TechnicianId { get; set; }
        public long? TeamId { get; set; }
        public long? TeamLeaderId { get; set; }
        public bool IsManagerOverride { get; set; }
    }
}
