using System;
using System.ComponentModel.DataAnnotations;
namespace MyGarage.DTOs.Requests
{
    public class InsuranceCompanyUpdateDto
    {
        public string? Name { get; set; }
        public string? TaxCode { get; set; }
        public decimal? CreditLimit { get; set; }
        public decimal? CurrentDebt { get; set; }
        public string? ContactPerson { get; set; }
        [RegularExpression(ValidationPatterns.VietnamPhone10, ErrorMessage = ValidationMessages.VietnamPhone10)]
        public string? Phone { get; set; }
        [EmailAddress(ErrorMessage = ValidationMessages.Email)]
        public string? Email { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
