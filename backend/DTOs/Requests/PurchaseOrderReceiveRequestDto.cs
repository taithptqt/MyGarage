using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Requests
{
    public sealed class PurchaseOrderReceiveRequestDto
    {
        public DateTime? ReceivedAt { get; set; }
        public string? Note { get; set; }
        public long? ReceivedByStaffId { get; set; }
        public List<PurchaseOrderReceiveItemDto> Items { get; set; } = new();
    }

    public sealed class PurchaseOrderReceiveItemDto
    {
        public long? PurchaseOrderItemId { get; set; }
        public long? ProductId { get; set; }
        public decimal ReceivedQuantity { get; set; }
        public decimal? CostPrice { get; set; }
    }
}
