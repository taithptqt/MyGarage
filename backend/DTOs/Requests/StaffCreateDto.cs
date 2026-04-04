using System;

namespace MyGarage.DTOs.Requests
{
    public class StaffCreateDto
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public long? RoleId { get; set; }
        public long? BranchId { get; set; }
        public long? StaffTierId { get; set; }
        public int? SkillLevel { get; set; }
        public string? CitizenId { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public DateOnly? HireDate { get; set; }
        public DateOnly? TerminationDate { get; set; }
        public string? AvatarUrl { get; set; }
        public int? AccessFailedCount { get; set; }
        public bool? IsLockedOut { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? Status { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
