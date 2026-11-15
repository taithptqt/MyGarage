using System.Collections.Generic;

namespace MyGarage.DTOs.Requests;

public sealed class QcTemplateGroupInputDto
{
    public string? Name { get; set; }
    public List<string>? Items { get; set; }
}

public sealed class QcTemplateItemInputDto
{
    public string? Category { get; set; }
    public string? ItemName { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsRequired { get; set; }
    public int? SortOrder { get; set; }
}

public sealed class QcTemplateUpsertDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ServiceCategory { get; set; }
    public bool? IsActive { get; set; }
    public List<QcTemplateGroupInputDto>? Groups { get; set; }
    public List<QcTemplateItemInputDto>? Items { get; set; }
}
