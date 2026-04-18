namespace MyGarage.DTOs.Requests;

public sealed class QuotationFinancialApprovalRequestDto
{
    public long ApprovedBy { get; set; }
    public bool Approved { get; set; } = true;
    public string? Note { get; set; }
}
