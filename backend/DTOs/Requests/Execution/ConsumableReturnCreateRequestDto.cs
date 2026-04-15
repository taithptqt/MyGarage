using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Execution
{
    public sealed class ConsumableReturnCreateRequestDto
    {
        [Required]
        public long ServiceOrderId { get; set; }

        [Required]
        public long TechnicianId { get; set; }

        public long? ReceivedBy { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public List<ConsumableReturnCreateItemDto> Items { get; set; } = new();
    }

    public sealed class ConsumableReturnCreateItemDto
    {
        [Required]
        public long ProductId { get; set; }

        public long? MaterialReqDetailId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal IssuedQuantity { get; set; }

        [Range(0, double.MaxValue)]
        public decimal UsedQuantity { get; set; }

        [MaxLength(50)]
        public string Unit { get; set; } = "EA";
    }
}
