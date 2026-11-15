namespace MyGarage.DTOs.Requests;

public sealed class DiagnosisForemanDecisionRequestDto
{
    public long ForemanId { get; set; }

    public bool Approve { get; set; }

    public string? Note { get; set; }
}
