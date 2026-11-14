using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class PackageResponseDto
    {
        public long Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public long CategoryId { get; set; }
        public string? CategoryCode { get; set; }
        public string? CategoryName { get; set; }
        public decimal BasePrice { get; set; }
        public int StandardMinutes { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<PackageItemResponseDto> Items { get; set; } = new();
    }
}
