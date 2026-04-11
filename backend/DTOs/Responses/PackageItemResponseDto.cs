namespace MyGarage.DTOs.Responses
{
    public class PackageItemResponseDto
    {
        public long Id { get; set; }
        public long PackageId { get; set; }
        public long StandardServiceId { get; set; }
        public string? StandardServiceCode { get; set; }
        public string? StandardServiceName { get; set; }
        public decimal Quantity { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsDefault { get; set; }
        public bool? IsActive { get; set; }
    }
}
