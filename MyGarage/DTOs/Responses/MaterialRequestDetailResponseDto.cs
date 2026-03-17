using System;
namespace MyGarage.DTOs.Responses
{
    public class MaterialRequestDetailResponseDto
    {
        public long Id { get; set; }
        public long RequestId { get; set; }
        public long ProductId { get; set; }
        public long WarehouseId { get; set; }
        public decimal? RequestedQuantity { get; set; }
        public string? RequestedCondition { get; set; }
        public decimal? IssuedQuantity { get; set; }
        public decimal? ReturnedQuantity { get; set; }
        public decimal? WastedQuantity { get; set; }
        public long? ServiceOrderDetailId { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
