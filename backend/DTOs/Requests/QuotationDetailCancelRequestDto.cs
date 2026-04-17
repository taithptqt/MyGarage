namespace MyGarage.DTOs.Requests;

public sealed class QuotationDetailCancelRequestDto
{
    public long ActorStaffId { get; set; }
    public string? CancelReason { get; set; }
}
