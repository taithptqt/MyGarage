using System;
namespace MyGarage.DTOs.Requests
{
    public class ServiceCategoryCreateDto
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public long? ParentCategoryId { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public int? DisplayOrder { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
