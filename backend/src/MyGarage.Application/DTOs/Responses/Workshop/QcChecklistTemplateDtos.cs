using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses;

public sealed record QcTemplateGroupDto(
    string Name,
    IReadOnlyList<string> Items);

public sealed record QcTemplateItemDto(
    long Id,
    long TemplateId,
    string? Category,
    string Name,
    string ItemName,
    string? Description,
    bool IsRequired,
    int SortOrder);

public sealed record QcTemplateDto(
    long Id,
    string Name,
    string? Description,
    string? ServiceCategory,
    bool IsActive,
    DateTime? CreatedAt,
    IReadOnlyList<QcTemplateGroupDto> Groups,
    IReadOnlyList<QcTemplateItemDto> Items);

public sealed record QcTemplateListEnvelopeDto(
    string ServiceType,
    int Total,
    IReadOnlyList<QcTemplateDto> Items);

public sealed record QcTemplateDeleteResultDto(
    string Message,
    long Id,
    bool IsActive);

public enum QcChecklistTemplateCommandStatus
{
    Ok = 0,
    Created = 1,
    NoContent = 2,
    BadRequest = 3,
    NotFound = 4
}

public sealed record QcChecklistTemplateCommandResult<T>(
    QcChecklistTemplateCommandStatus Status,
    T? Data = default,
    string? ErrorMessage = null)
{
    public static QcChecklistTemplateCommandResult<T> Ok(T data)
        => new(QcChecklistTemplateCommandStatus.Ok, data, null);

    public static QcChecklistTemplateCommandResult<T> Created(T data)
        => new(QcChecklistTemplateCommandStatus.Created, data, null);

    public static QcChecklistTemplateCommandResult<T> NoContent()
        => new(QcChecklistTemplateCommandStatus.NoContent, default, null);

    public static QcChecklistTemplateCommandResult<T> BadRequest(string errorMessage)
        => new(QcChecklistTemplateCommandStatus.BadRequest, default, errorMessage);

    public static QcChecklistTemplateCommandResult<T> NotFound(string errorMessage)
        => new(QcChecklistTemplateCommandStatus.NotFound, default, errorMessage);
}
