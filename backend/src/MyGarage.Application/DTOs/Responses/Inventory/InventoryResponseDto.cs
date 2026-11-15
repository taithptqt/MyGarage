using System;
namespace MyGarage.DTOs.Responses
{
    public class InventoryResponseDto
    {
        public long Id { get; set; }
        public long WarehouseId { get; set; }
        public long ProductId { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? MinStockLevel { get; set; }
        public decimal? MaxStockLevel { get; set; }
        public string? LocationBin { get; set; }
        public decimal? ReservedQuantity { get; set; }
        public decimal? CurrentMacPrice { get; set; }
        public decimal? TotalInventoryValue { get; set; }
    }
}
