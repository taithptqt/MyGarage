using System;
namespace MyGarage.DTOs.Requests
{
    public class InsuranceCompanyCreateDto
    {
        public string? Name { get; set; }
        public string? TaxCode { get; set; }
        public decimal? CreditLimit { get; set; }
        public decimal? CurrentDebt { get; set; }
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
