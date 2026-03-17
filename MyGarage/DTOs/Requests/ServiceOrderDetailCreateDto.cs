using System;
namespace MyGarage.DTOs.Requests
{
    public class ServiceOrderDetailCreateDto
    {
        public long ServiceOrderId { get; set; }
        public long? DiagnoseRecommendationId { get; set; }
        public long? StandardServiceId { get; set; }
        public long? TechnicianId { get; set; }
        public long? OriginalOrderDetailId { get; set; }
        public long? SupplierId { get; set; }
        public string? ItemType { get; set; }
        public long? ItemId { get; set; }
        public string? ItemName { get; set; }
        public string? Description { get; set; }
        public decimal? BuyoutCost { get; set; }
        public bool? IsWarrantyItem { get; set; }
        public bool? IsCustomerBroughtPart { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? IssuedQuantity { get; set; }
        public decimal? CostPriceAtTime { get; set; }
        public decimal? UnitPriceAtTime { get; set; }
        public string? Unit { get; set; }
        public decimal? SubTotal { get; set; }
        public decimal? DiscountValue { get; set; }
        public decimal? DiscountAllocatedAmount { get; set; }
        public decimal? TaxRate { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? FinalAmount { get; set; }
        public string? PayerType { get; set; }
        public long? InsuranceCompanyId { get; set; }
        public decimal? InsuranceRequestedAmount { get; set; }
        public decimal? InsuranceApprovedAmount { get; set; }
        public decimal? CustomerCoPayAmount { get; set; }
        public string? InsuranceStatus { get; set; }
        public decimal? GarageAbsorbedAmount { get; set; }
        public long? AbsorbedApprovedBy { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
        public bool? IsReturnOldPart { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
