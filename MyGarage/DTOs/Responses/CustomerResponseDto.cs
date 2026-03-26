using System;

namespace MyGarage.DTOs.Responses
{
    public class CustomerResponseDto
    {
        public long Id { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? GoogleId { get; set; }
        public string? Type { get; set; }
        public long? TierId { get; set; }
        public decimal? TotalSpending { get; set; }
        public int? NoShowCount { get; set; }
        public bool? IsActive { get; set; }
        public string? Note { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
