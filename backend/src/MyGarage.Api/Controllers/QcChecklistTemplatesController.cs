using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers;

[Route("api/qc-checklist-templates")]
[Route("api/v1/qc-checklist-templates")]
[ApiController]
[Authorize]
public sealed class QcChecklistTemplatesController : ControllerBase
{
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

    private readonly IGarageContext _db;

    public QcChecklistTemplatesController(IGarageContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetTemplates(
        [FromQuery] string? serviceType = null,
        [FromQuery] long? serviceOrderId = null,
        [FromQuery] bool includeInactive = false)
    {
        var resolvedServiceType = (serviceType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(resolvedServiceType) && serviceOrderId.HasValue && serviceOrderId.Value > 0)
        {
            resolvedServiceType = await _db.ServiceOrders
                .AsNoTracking()
                .Where(so => so.Id == serviceOrderId.Value)
                .Select(so => so.OrderCategory ?? string.Empty)
                .FirstOrDefaultAsync() ?? string.Empty;
        }

        var rows = await QueryTemplatesAsync(resolvedServiceType, includeInactive);
        return Ok(new
        {
            serviceType = resolvedServiceType,
            total = rows.Count,
            items = rows
        });
    }

    [HttpGet("/api/qc-templates")]
    [HttpGet("/api/v1/qc-templates")]
    public async Task<IActionResult> GetTemplatesList(
        [FromQuery] string? serviceType = null,
        [FromQuery] bool includeInactive = true)
    {
        var rows = await QueryTemplatesAsync((serviceType ?? string.Empty).Trim(), includeInactive);
        return Ok(rows);
    }

    [HttpGet("{id:long}")]
    [HttpGet("/api/qc-templates/{id:long}")]
    [HttpGet("/api/v1/qc-templates/{id:long}")]
    public async Task<IActionResult> GetTemplateById(long id)
    {
        var entity = await _db.QcChecklistTemplates
            .AsNoTracking()
            .Include(t => t.TemplateItems.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .FirstOrDefaultAsync(t => t.Id == id);

        if (entity == null)
        {
            return NotFound(new { message = "Không tìm thấy template QC." });
        }

        return Ok(MapTemplate(entity));
    }

    [HttpPost]
    [HttpPost("/api/qc-templates")]
    [HttpPost("/api/v1/qc-templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] QcTemplateUpsertDto? payload)
    {
        if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC", "QC"))
        {
            return Forbid();
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.Name))
        {
            return BadRequest(new { message = "Tên template QC là bắt buộc." });
        }

        var now = VietnamTime.Now;
        var entity = new QcChecklistTemplate
        {
            Name = payload.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(payload.Description) ? null : payload.Description.Trim(),
            ServiceCategory = string.IsNullOrWhiteSpace(payload.ServiceCategory) ? null : payload.ServiceCategory.Trim(),
            IsActive = payload.IsActive ?? true,
            CreatedAt = now,
            TemplateItems = BuildTemplateItems(payload, now)
        };

        _db.QcChecklistTemplates.Add(entity);
        await _db.SaveChangesAsync();

        var created = await _db.QcChecklistTemplates
            .AsNoTracking()
            .Include(t => t.TemplateItems.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .FirstAsync(t => t.Id == entity.Id);

        return CreatedAtAction(nameof(GetTemplateById), new { id = created.Id }, MapTemplate(created));
    }

    [HttpPut("{id:long}")]
    [HttpPut("/api/qc-templates/{id:long}")]
    [HttpPut("/api/v1/qc-templates/{id:long}")]
    public async Task<IActionResult> UpdateTemplate(long id, [FromBody] QcTemplateUpsertDto? payload)
    {
        if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC", "QC"))
        {
            return Forbid();
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.Name))
        {
            return BadRequest(new { message = "Tên template QC là bắt buộc." });
        }

        var entity = await _db.QcChecklistTemplates
            .Include(t => t.TemplateItems)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (entity == null)
        {
            return NotFound(new { message = "Không tìm thấy template QC." });
        }

        entity.Name = payload.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(payload.Description) ? null : payload.Description.Trim();
        entity.ServiceCategory = string.IsNullOrWhiteSpace(payload.ServiceCategory) ? null : payload.ServiceCategory.Trim();
        if (payload.IsActive.HasValue)
        {
            entity.IsActive = payload.IsActive.Value;
        }

        if (payload.Groups != null || payload.Items != null)
        {
            _db.QcChecklistTemplateItems.RemoveRange(entity.TemplateItems);
            entity.TemplateItems = BuildTemplateItems(payload, VietnamTime.Now);
        }

        await _db.SaveChangesAsync();

        var updated = await _db.QcChecklistTemplates
            .AsNoTracking()
            .Include(t => t.TemplateItems.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .FirstAsync(t => t.Id == entity.Id);

        return Ok(MapTemplate(updated));
    }

    [HttpDelete("{id:long}")]
    [HttpDelete("/api/qc-templates/{id:long}")]
    [HttpDelete("/api/v1/qc-templates/{id:long}")]
    public async Task<IActionResult> DeleteTemplate(long id)
    {
        if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC", "QC"))
        {
            return Forbid();
        }

        var entity = await _db.QcChecklistTemplates
            .FirstOrDefaultAsync(t => t.Id == id);
        if (entity == null)
        {
            return NotFound(new { message = "Không tìm thấy template QC." });
        }

        var inUse = await _db.QcInspections
            .AsNoTracking()
            .AnyAsync(i => i.TemplateId == id);

        if (inUse)
        {
            entity.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Template đang được tham chiếu bởi phiếu QC nên đã chuyển sang ngưng hoạt động.", id = entity.Id, isActive = false });
        }

        _db.QcChecklistTemplates.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<List<object>> QueryTemplatesAsync(string normalizedServiceType, bool includeInactive)
    {
        var query = _db.QcChecklistTemplates
            .AsNoTracking()
            .Include(x => x.TemplateItems)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(normalizedServiceType))
        {
            var key = normalizedServiceType.ToUpperInvariant();
            query = query.Where(x =>
                string.IsNullOrWhiteSpace(x.ServiceCategory) ||
                x.ServiceCategory!.ToUpper() == key);
        }

        var entities = await query
            .OrderBy(x => string.IsNullOrWhiteSpace(x.ServiceCategory) ? 1 : 0)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return entities.Select(MapTemplate).ToList();
    }

    private static List<QcChecklistTemplateItem> BuildTemplateItems(QcTemplateUpsertDto payload, DateTime now)
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
                    Category = string.IsNullOrWhiteSpace(raw.Category) ? "Tổng quát" : raw.Category.Trim(),
                    ItemName = name,
                    Description = string.IsNullOrWhiteSpace(raw.Description) ? null : raw.Description.Trim(),
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
                var category = string.IsNullOrWhiteSpace(group.Name) ? "Tổng quát" : group.Name.Trim();
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

    private static object MapTemplate(QcChecklistTemplate entity)
    {
        var orderedItems = entity.TemplateItems
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .ToList();

        var groups = orderedItems
            .GroupBy(i => string.IsNullOrWhiteSpace(i.Category) ? "Tổng quát" : i.Category.Trim())
            .Select(g => new
            {
                name = g.Key,
                items = g.Select(x => x.ItemName).ToList()
            })
            .ToList();

        return new
        {
            id = entity.Id,
            name = entity.Name,
            description = entity.Description,
            serviceCategory = entity.ServiceCategory,
            isActive = entity.IsActive,
            createdAt = entity.CreatedAt,
            groups,
            items = orderedItems.Select(i => new
            {
                id = i.Id,
                templateId = i.TemplateId,
                category = i.Category,
                name = i.ItemName,
                itemName = i.ItemName,
                description = i.Description,
                isRequired = i.IsRequired,
                sortOrder = i.SortOrder
            }).ToList()
        };
    }
}

