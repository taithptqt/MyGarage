using System;
namespace MyGarage.DTOs.Responses
{
    public class StandardServiceResponseDto
    {
        public long Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public long CategoryId { get; set; }
        public decimal? BasePrice { get; set; }
        public string? EngineType { get; set; }
        public int StandardMinutes { get; set; }
        public decimal? TaxRate { get; set; }
        public int? WarrantyMonths { get; set; }
        public int? WarrantyKm { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? ConsumableCost { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
