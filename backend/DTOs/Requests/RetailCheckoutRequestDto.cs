namespace MyGarage.DTOs.Requests
{
    public class RetailCheckoutRequestDto
    {
        public long BranchId { get; set; }
        public string? CustomerNotes { get; set; }
        public long? VoucherId { get; set; }
        public string? VoucherCode { get; set; }
        public long? PaymentMethodId { get; set; }
        public string? Channel { get; set; }
        public List<RetailCheckoutItemRequestDto> Items { get; set; } = new();
    }

    public class RetailCheckoutItemRequestDto
    {
        public long ProductId { get; set; }
        public int Quantity { get; set; }
    }
}

