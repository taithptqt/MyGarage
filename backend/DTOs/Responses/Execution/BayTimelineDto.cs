using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses.Execution
{
    public class BayTimelineDto
    {
        public long BayId { get; set; }
        public string BayName { get; set; } = string.Empty;
        public string ServiceArea { get; set; } = string.Empty;
        public string OperationalStatus { get; set; } = string.Empty;
        
        public List<BayAllocationDto> Allocations { get; set; } = new List<BayAllocationDto>();
    }
}
