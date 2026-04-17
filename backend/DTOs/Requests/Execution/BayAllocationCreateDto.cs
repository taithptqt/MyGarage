using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Execution
{
    public class BayAllocationCreateDto
    {
        [Required]
        public long BayId { get; set; }

        [Required]
        public long ServiceOrderId { get; set; }

        [Required]
        public long VehicleId { get; set; }

        public int? PlannedDuration { get; set; }

        public string? Notes { get; set; }
    }
}
