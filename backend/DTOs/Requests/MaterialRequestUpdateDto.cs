using System;
namespace MyGarage.DTOs.Requests
{
    public class MaterialRequestUpdateDto
    {
        public long? ServiceOrderId { get; set; }
        public long? TechnicianId { get; set; }
        public long? StorekeeperId { get; set; }
        public DateTime? RequestDate { get; set; }
        public string? Status { get; set; }
        public bool? IsRework { get; set; }
        public string? Note { get; set; }
    }
}
