using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class QcChecklistTemplateCommandService : IQcChecklistTemplateCommandService
{
    private const string DefaultCategoryName = "Tổng quát";
    private const string NameRequiredMessage = "Tên template QC là bắt buộc.";
    private const string TemplateNotFoundMessage = "Không tìm thấy template QC.";
    private const string TemplateDeactivatedMessage = "Template đang được tham chiếu bởi phiếu QC nên đã chuyển sang ngưng hoạt động.";

    private readonly IQcChecklistTemplateDataService _dataService;

    public QcChecklistTemplateCommandService(IQcChecklistTemplateDataService dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
    }

    public async Task<QcTemplateListEnvelopeDto> GetTemplatesAsync(
        string? serviceType = null,
        long? serviceOrderId = null,
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        var resolvedServiceType = (serviceType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(resolvedServiceType) && serviceOrderId.HasValue && serviceOrderId.Value > 0)
        {
            resolvedServiceType = await _dataService.GetServiceOrderCategoryAsync(serviceOrderId.Value, ct);
        }

        var rows = await QueryTemplatesAsync(resolvedServiceType, includeInactive, ct);
        return new QcTemplateListEnvelopeDto(resolvedServiceType, rows.Count, rows);
    }

    public async Task<IReadOnlyList<QcTemplateDto>> GetTemplatesListAsync(
        string? serviceType = null,
        bool includeInactive = true,
        CancellationToken ct = default)
    {
        return await QueryTemplatesAsync((serviceType ?? string.Empty).Trim(), includeInactive, ct);
    }

    public async Task<QcChecklistTemplateCommandResult<QcTemplateDto>> GetByIdAsync(
        long id,
        CancellationToken ct = default)
    {
        var entity = await _dataService.GetByIdAsync(id, ct);
        if (entity == null)
        {
            return QcChecklistTemplateCommandResult<QcTemplateDto>.NotFound(TemplateNotFoundMessage);
        }

        return QcChecklistTemplateCommandResult<QcTemplateDto>.Ok(MapTemplate(entity));
    }

    public async Task<QcChecklistTemplateCommandResult<QcTemplateDto>> CreateAsync(
        QcTemplateUpsertDto? payload,
        CancellationToken ct = default)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Name))
        {
            return QcChecklistTemplateCommandResult<QcTemplateDto>.BadRequest(NameRequiredMessage);
        }

        var now = VietnamTime.Now;
        var entity = new QcChecklistTemplate
        {
            Name = payload.Name.Trim(),
            Description = NormalizeOptionalText(payload.Description),
            ServiceCategory = NormalizeOptionalText(payload.ServiceCategory),
            IsActive = payload.IsActive ?? true,
            CreatedAt = now,
            TemplateItems = BuildTemplateItems(payload, now)
        };

        var created = await _dataService.CreateAsync(entity, ct);
        return QcChecklistTemplateCommandResult<QcTemplateDto>.Created(MapTemplate(created));
    }

    public async Task<QcChecklistTemplateCommandResult<QcTemplateDto>> UpdateAsync(
        long id,
        QcTemplateUpsertDto? payload,
        CancellationToken ct = default)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Name))
        {
            return QcChecklistTemplateCommandResult<QcTemplateDto>.BadRequest(NameRequiredMessage);
        }

        var replacementItems = (payload.Groups != null || payload.Items != null)
            ? BuildTemplateItems(payload, VietnamTime.Now)
            : null;

        var updated = await _dataService.UpdateAsync(
            id,
            payload.Name.Trim(),
            NormalizeOptionalText(payload.Description),
            NormalizeOptionalText(payload.ServiceCategory),
            payload.IsActive,
            replacementItems,
            ct);

        if (updated == null)
        {
            return QcChecklistTemplateCommandResult<QcTemplateDto>.NotFound(TemplateNotFoundMessage);
        }

        return QcChecklistTemplateCommandResult<QcTemplateDto>.Ok(MapTemplate(updated));
    }

    public async Task<QcChecklistTemplateCommandResult<QcTemplateDeleteResultDto>> DeleteAsync(
        long id,
        CancellationToken ct = default)
    {
        var (found, deactivated) = await _dataService.DeleteOrDeactivateAsync(id, ct);
        if (!found)
        {
            return QcChecklistTemplateCommandResult<QcTemplateDeleteResultDto>.NotFound(TemplateNotFoundMessage);
        }

        if (deactivated)
        {
            return QcChecklistTemplateCommandResult<QcTemplateDeleteResultDto>.Ok(
                new QcTemplateDeleteResultDto(TemplateDeactivatedMessage, id, false));
        }

        return QcChecklistTemplateCommandResult<QcTemplateDeleteResultDto>.NoContent();
    }

    private async Task<List<QcTemplateDto>> QueryTemplatesAsync(
        string normalizedServiceType,
        bool includeInactive,
        CancellationToken ct)
    {
        var entities = await _dataService.GetTemplatesAsync(normalizedServiceType, includeInactive, ct);
        return entities.Select(MapTemplate).ToList();
    }

    internal static List<QcChecklistTemplateItem> BuildTemplateItems(QcTemplateUpsertDto payload, DateTime now)
    {
        var items = new List<QcChecklistTemplateItem>();
        var order = 1;

        if (payload.Items != null && payload.Items.Count > 0)
        {
            foreach (var raw in payload.Items.Where(x => x != null))
            {
                var name = (raw.ItemName ?? raw.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                items.Add(new QcChecklistTemplateItem
                {
                    Category = string.IsNullOrWhiteSpace(raw.Category) ? DefaultCategoryName : raw.Category.Trim(),
                    ItemName = name,
                    Description = NormalizeOptionalText(raw.Description),
                    IsRequired = raw.IsRequired ?? true,
                    SortOrder = raw.SortOrder ?? order++,
                    CreatedAt = now
                });
            }
        }
        else if (payload.Groups != null && payload.Groups.Count > 0)
        {
            foreach (var group in payload.Groups.Where(g => g != null))
            {
                var category = string.IsNullOrWhiteSpace(group.Name) ? DefaultCategoryName : group.Name.Trim();
                foreach (var rawName in group.Items ?? Enumerable.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(rawName))
                    {
                        continue;
                    }

                    items.Add(new QcChecklistTemplateItem
                    {
                        Category = category,
                        ItemName = rawName.Trim(),
                        IsRequired = true,
                        SortOrder = order++,
                        CreatedAt = now
                    });
                }
            }
        }

        return items;
    }

    internal static QcTemplateDto MapTemplate(QcChecklistTemplate entity)
    {
        var orderedItems = (entity.TemplateItems ?? Array.Empty<QcChecklistTemplateItem>())
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .ToList();

        var groups = orderedItems
            .GroupBy(i => string.IsNullOrWhiteSpace(i.Category) ? DefaultCategoryName : i.Category.Trim())
            .Select(g => new QcTemplateGroupDto(
                g.Key,
                g.Select(x => x.ItemName).ToList()))
            .ToList();

        var items = orderedItems.Select(i => new QcTemplateItemDto(
            i.Id,
            i.TemplateId,
            i.Category,
            i.ItemName,
            i.ItemName,
            i.Description,
            i.IsRequired,
            i.SortOrder)).ToList();

        return new QcTemplateDto(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.ServiceCategory,
            entity.IsActive,
            entity.CreatedAt,
            groups,
            items);
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
