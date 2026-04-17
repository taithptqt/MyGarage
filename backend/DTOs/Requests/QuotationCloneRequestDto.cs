using System.Collections.Generic;

namespace MyGarage.DTOs.Requests;

public sealed class QuotationCloneRequestDto
{
    public long ActorStaffId { get; set; }

    public string? Note { get; set; }

    public List<long> RemoveQuotationDetailIds { get; set; } = new();
}
