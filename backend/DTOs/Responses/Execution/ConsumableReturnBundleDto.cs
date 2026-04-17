using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses.Execution
{
    public sealed class ConsumableReturnBundleDto
    {
        public long Id { get; set; }
        public string BundleCode { get; set; } = string.Empty;
        public long ServiceOrderId { get; set; }
        public long TechnicianId { get; set; }
        public long? ReceivedBy { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime ReturnedAt { get; set; }
        public List<ConsumableReturnBundleItemDto> Items { get; set; } = new();
    }

    public sealed class ConsumableReturnBundleItemDto
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public long? MaterialReqDetailId { get; set; }
        public decimal IssuedQuantity { get; set; }
        public decimal UsedQuantity { get; set; }
        public decimal ReturnedQuantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}
