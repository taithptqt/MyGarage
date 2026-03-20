using System;
namespace MyGarage.DTOs.Responses
{
    public class ServiceCategoryResponseDto
    {
        public long Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public long? ParentCategoryId { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public int? DisplayOrder { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
