using System;
namespace MyGarage.DTOs.Responses
{
    public class MaterialRequestResponseDto
    {
        public long Id { get; set; }
        public long? ServiceOrderId { get; set; }
        public long? TechnicianId { get; set; }
        public long? StorekeeperId { get; set; }
        public DateTime? RequestDate { get; set; }
        public string? Status { get; set; }
        public bool? IsRework { get; set; }
        public string? Note { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
