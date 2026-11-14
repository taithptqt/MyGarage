using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Requests;

public sealed class QcInspectionItemsUpsertRequestDto
{
    public bool OverwriteExisting { get; set; }
    public List<QcInspectionChecklistItemUpsertDto> Items { get; set; } = [];
}

public sealed class QcInspectionChecklistItemUpsertDto
{
    public long? Id { get; set; }
    public long? TemplateItemId { get; set; }
    public string? ItemName { get; set; }
    public string? Category { get; set; }
    public bool? IsRequired { get; set; }
    public string? Result { get; set; }
    public string? FailNote { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime? CheckedAt { get; set; }
    public long? CheckedBy { get; set; }
}

