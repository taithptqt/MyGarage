using System.Collections.Generic;

namespace MyGarage.DTOs.Requests
{
    public class ServiceOrderBatchCreateRequestDto
    {
        public bool CreateGroupSession { get; set; }
        public long? CustomerId { get; set; }
        public long? BranchId { get; set; }
        public string? CustomerNote { get; set; }
        public List<WorkshopServiceOrderCreateDto> Items { get; set; } = new();
    }
}
