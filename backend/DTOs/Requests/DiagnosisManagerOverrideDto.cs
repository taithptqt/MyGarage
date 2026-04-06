namespace MyGarage.DTOs.Requests
{
    public class DiagnosisManagerOverrideDto
    {
        public long ManagerId { get; set; }
        public long TechnicianId { get; set; }
        public long? TeamId { get; set; }
        public string? Reason { get; set; }
    }
}
