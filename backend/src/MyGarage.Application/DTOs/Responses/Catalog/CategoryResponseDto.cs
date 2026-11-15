using System;
namespace MyGarage.DTOs.Responses
{
    public class CategoryResponseDto
    {
        public long Id { get; set; }
        public string? CategoryName { get; set; }
        public long? ParentId { get; set; }
        public bool? IsDepositRequired { get; set; }
        public string? Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
