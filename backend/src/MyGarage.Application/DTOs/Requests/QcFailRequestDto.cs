namespace MyGarage.DTOs.Requests
{
    public class QcFailRequestDto
    {
        public long InspectorId { get; set; }
        public string Reason { get; set; } = null!;
        public long? AssignToTechnicianId { get; set; }
    }
}
