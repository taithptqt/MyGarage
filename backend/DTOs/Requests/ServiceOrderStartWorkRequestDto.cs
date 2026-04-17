namespace MyGarage.DTOs.Requests
{
    public sealed class ServiceOrderStartWorkRequestDto
    {
        public long? TechnicianId { get; set; }
        public string? Note { get; set; }
    }
}
