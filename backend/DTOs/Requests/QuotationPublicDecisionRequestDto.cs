namespace MyGarage.DTOs.Requests;

public sealed class QuotationPublicDecisionRequestDto
{
    public bool Approved { get; set; }
    public string? Note { get; set; }
}

