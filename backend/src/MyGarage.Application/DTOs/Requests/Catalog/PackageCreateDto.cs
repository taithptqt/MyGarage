using System.Collections.Generic;

namespace MyGarage.DTOs.Requests
{
    public class PackageCreateDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long CategoryId { get; set; }
        public decimal BasePrice { get; set; }
        public int StandardMinutes { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool? IsActive { get; set; }
        public List<PackageItemRequestDto> Items { get; set; } = new();
    }
}
