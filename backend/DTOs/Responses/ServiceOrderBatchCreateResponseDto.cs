using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class ServiceOrderBatchCreateResponseDto
    {
        public long? GroupSessionId { get; set; }
        public int TotalCreated { get; set; }
        public List<ServiceOrderResponseDto> Orders { get; set; } = new();
    }
}
