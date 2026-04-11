namespace MyGarage.DTOs.Requests
{
    public class PackageItemRequestDto
    {
        public long StandardServiceId { get; set; }
        public decimal Quantity { get; set; } = 1m;
        public int? SortOrder { get; set; }
        public bool? IsDefault { get; set; }
    }
}
