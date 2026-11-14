namespace MyGarage.DTOs.Requests;

public sealed class QuotationCustomerLinkPublishRequestDto
{
    public long ActorStaffId { get; set; }
    public string? Note { get; set; }
    public string? Email { get; set; }
    public bool SendEmail { get; set; }
    public string? IdempotencyKey { get; set; }
}
