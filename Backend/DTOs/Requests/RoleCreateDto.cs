using System;
namespace MyGarage.DTOs.Requests
{
    public class RoleCreateDto
    {
        public string? RoleName { get; set; }
        public string? RoleCode { get; set; }
        public string? Scope { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
