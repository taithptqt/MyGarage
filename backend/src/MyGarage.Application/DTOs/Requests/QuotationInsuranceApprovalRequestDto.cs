using System.Text.Json.Serialization;

namespace MyGarage.DTOs.Requests;

public sealed class QuotationInsuranceApprovalRequestDto
{
    [JsonPropertyName("actorStaffId")]
    public long ActorStaffId { get; set; }

    [JsonPropertyName("insuranceApprovalEvidenceUrl")]
    public string InsuranceApprovalEvidenceUrl { get; set; } = null!;

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }
}
