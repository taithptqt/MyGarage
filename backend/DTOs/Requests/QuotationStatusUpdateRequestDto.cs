namespace MyGarage.DTOs.Requests;

public sealed class QuotationStatusUpdateRequestDto
{
    public long ActorStaffId { get; set; }
    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }
}
