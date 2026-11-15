using System.Text.Json.Serialization;

namespace MyGarage.DTOs.Requests;

public sealed class QuotationPublicOtpRequestDto
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }
}
