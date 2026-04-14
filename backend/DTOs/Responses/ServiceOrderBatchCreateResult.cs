using System.Collections.Generic;
using MyGarage.Models;

namespace MyGarage.DTOs.Responses
{
    public class ServiceOrderBatchCreateResult
    {
        public long? GroupSessionId { get; set; }
        public List<ServiceOrder> Orders { get; set; } = new();
    }
}
