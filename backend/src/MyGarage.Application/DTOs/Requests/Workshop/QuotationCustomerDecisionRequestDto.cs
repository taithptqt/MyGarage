namespace MyGarage.DTOs.Requests;

public sealed class QuotationCustomerDecisionRequestDto
{
    public bool Approved { get; set; }
    public long? CustomerId { get; set; }
    public long? ActorStaffId { get; set; }
    public string? Note { get; set; }
}
