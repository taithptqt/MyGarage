namespace MyGarage.DTOs.Requests;

public sealed class MandatoryOverrideDecisionRequestDto
{
    public long DecidedBy { get; set; }

    public bool Approve { get; set; }

    public string? Note { get; set; }
}
