using System;
namespace MyGarage.DTOs.Requests
{
    public class RetailReturnItemCreateDto
    {
        public long RetailReturnId { get; set; }
        public long RetailOrderItemId { get; set; }
        public long WarehouseId { get; set; }
        public decimal? ReturnedQuantity { get; set; }
        public string? ItemCondition { get; set; }
        public decimal? RefundAmount { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
        public string? ConditionEvidenceUrls { get; set; }
        public long CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
