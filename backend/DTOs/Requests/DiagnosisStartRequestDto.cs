namespace MyGarage.DTOs.Requests
{
    public class DiagnosisStartRequestDto
    {
        public long TechnicianId { get; set; }
        public long? BayId { get; set; }
    }
}
