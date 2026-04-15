using System.Text.Json.Serialization;

namespace MyGarage.DTOs.Requests;

public sealed class QuotationPublicDecisionRequestDto
{
    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("otpCode")]
    public string? OtpCode { get; set; }
}
