using System;
namespace MyGarage.DTOs.Requests
{
    public class SupplierCreateDto
    {
        public string? SupplierName { get; set; }
        public string? ContactName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public decimal? CurrentDebt { get; set; }
        public string? TaxCode { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
