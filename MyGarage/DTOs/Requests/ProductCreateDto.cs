using System;
namespace MyGarage.DTOs.Requests
{
    public class ProductCreateDto
    {
        public string? Sku { get; set; }
        public string? Name { get; set; }
        public string? Unit { get; set; }
        public long? CategoryId { get; set; }
        public decimal? CostPrice { get; set; }
        public decimal? RetailPrice { get; set; }
        public decimal? ServicePrice { get; set; }
        public decimal? TaxRate { get; set; }
        public int? WarrantyMonths { get; set; }
        public bool? IsStrictCapacityControl { get; set; }
        public bool? RequireEmptyReturn { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
