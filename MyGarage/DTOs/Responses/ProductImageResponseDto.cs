using System;
namespace MyGarage.DTOs.Responses
{
    public class ProductImageResponseDto
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public string? ImageUrl { get; set; }
        public bool? IsMain { get; set; }
        public int? SortOrder { get; set; }
        public string? Description { get; set; }
    }
}
