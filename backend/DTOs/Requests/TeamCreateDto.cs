using System;

namespace MyGarage.DTOs.Requests
{
    public class TeamCreateDto
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public long BranchId { get; set; }
        public string? SpecialtyTag { get; set; }
        public long? LeaderId { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
