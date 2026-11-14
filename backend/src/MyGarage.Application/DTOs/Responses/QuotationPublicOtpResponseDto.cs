using System;

namespace MyGarage.DTOs.Responses;

public sealed class QuotationPublicOtpResponseDto
{
    public bool Sent { get; set; }
    public string MaskedEmail { get; set; } = string.Empty;
    public DateTime OtpExpiresAt { get; set; }
    public int RetryAfterSeconds { get; set; }
}
