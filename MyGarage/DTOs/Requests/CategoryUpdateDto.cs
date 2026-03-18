using System;
namespace MyGarage.DTOs.Requests
{
    public class CategoryUpdateDto
    {
        public string? CategoryName { get; set; }
        public long? ParentId { get; set; }
        public bool? IsDepositRequired { get; set; }
        public string? Description { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
