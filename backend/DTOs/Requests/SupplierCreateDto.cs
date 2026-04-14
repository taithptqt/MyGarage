using System;
using System.ComponentModel.DataAnnotations;
namespace MyGarage.DTOs.Requests
{
    public class SupplierCreateDto
    {
        public string? SupplierName { get; set; }
        public string? ContactName { get; set; }
        [RegularExpression(ValidationPatterns.VietnamPhone10, ErrorMessage = ValidationMessages.VietnamPhone10)]
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
