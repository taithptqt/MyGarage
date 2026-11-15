using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests;

public sealed class MaterialRequestIssuanceDto
{
    [Required]
    public long RequestId { get; set; }

    [Required]
    public long ActorStaffId { get; set; }

    public string? Note { get; set; }

    [Required]
    public List<MaterialRequestIssuanceItemDto> Items { get; set; } = new();
}

public sealed class MaterialRequestIssuanceItemDto
{
    [Required]
    public long DetailId { get; set; }

    [Range(0.001, 1000000)]
    public decimal QuantityToIssue { get; set; }
}
