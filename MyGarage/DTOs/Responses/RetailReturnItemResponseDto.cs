using System;
namespace MyGarage.DTOs.Responses
{
    public class RetailReturnItemResponseDto
    {
        public long Id { get; set; }
        public long RetailReturnId { get; set; }
        public long RetailOrderItemId { get; set; }
        public long WarehouseId { get; set; }
        public decimal? ReturnedQuantity { get; set; }
        public string? ItemCondition { get; set; }
        public decimal? RefundAmount { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
        public string? ConditionEvidenceUrls { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
