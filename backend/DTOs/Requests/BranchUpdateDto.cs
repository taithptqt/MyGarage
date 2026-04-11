using System;
using System.ComponentModel.DataAnnotations;
namespace MyGarage.DTOs.Requests
{
    public class BranchUpdateDto
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        [RegularExpression(ValidationPatterns.VietnamPhone10, ErrorMessage = ValidationMessages.VietnamPhone10)]
        public string? Hotline { get; set; }
        public long? ManagerId { get; set; }
        [EmailAddress(ErrorMessage = ValidationMessages.Email)]
        public string? Email { get; set; }
        public string? Status { get; set; }
        public string? TaxCode { get; set; }
        public int? MaxWorkSlots { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Description { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
