using System;

namespace MyGarage.DTOs.Responses
{
    public class TeamResponseDto
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public long BranchId { get; set; }
        public string? SpecialtyTag { get; set; }
        public long? LeaderId { get; set; }
        public string? Status { get; set; }
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
