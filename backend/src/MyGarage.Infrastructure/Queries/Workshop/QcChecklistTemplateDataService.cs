using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfQcChecklistTemplateDataService(IGarageContext db) : IQcChecklistTemplateDataService
{
    public async Task<string> GetServiceOrderCategoryAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return await db.ServiceOrders
            .AsNoTracking()
            .Where(so => so.Id == serviceOrderId)
            .Select(so => so.OrderCategory ?? string.Empty)
            .FirstOrDefaultAsync(ct) ?? string.Empty;
    }

    public async Task<List<QcChecklistTemplate>> GetTemplatesAsync(
        string normalizedServiceType,
        bool includeInactive,
        CancellationToken ct = default)
    {
        var query = db.QcChecklistTemplates
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

        return await query
            .OrderBy(x => string.IsNullOrWhiteSpace(x.ServiceCategory) ? 1 : 0)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }

    public Task<QcChecklistTemplate?> GetByIdAsync(long id, CancellationToken ct = default) =>
        db.QcChecklistTemplates
            .AsNoTracking()
            .Include(t => t.TemplateItems.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<QcChecklistTemplate> CreateAsync(QcChecklistTemplate entity, CancellationToken ct = default)
    {
        db.QcChecklistTemplates.Add(entity);
        await db.SaveChangesAsync(ct);

        return await db.QcChecklistTemplates
            .AsNoTracking()
            .Include(t => t.TemplateItems.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .FirstAsync(t => t.Id == entity.Id, ct);
    }

    public async Task<QcChecklistTemplate?> UpdateAsync(
        long id,
        string name,
        string? description,
        string? serviceCategory,
        bool? isActive,
        List<QcChecklistTemplateItem>? replacementItems,
        CancellationToken ct = default)
    {
        var entity = await db.QcChecklistTemplates
            .Include(t => t.TemplateItems)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity == null)
        {
            return null;
        }

        entity.Name = name;
        entity.Description = description;
        entity.ServiceCategory = serviceCategory;
        if (isActive.HasValue)
        {
            entity.IsActive = isActive.Value;
        }

        if (replacementItems != null)
        {
            db.QcChecklistTemplateItems.RemoveRange(entity.TemplateItems);
            entity.TemplateItems = replacementItems;
        }

        await db.SaveChangesAsync(ct);

        return await db.QcChecklistTemplates
            .AsNoTracking()
            .Include(t => t.TemplateItems.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
            .FirstAsync(t => t.Id == entity.Id, ct);
    }

    public async Task<(bool Found, bool Deactivated)> DeleteOrDeactivateAsync(long id, CancellationToken ct = default)
    {
        var entity = await db.QcChecklistTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity == null)
        {
            return (false, false);
        }

        var inUse = await db.QcInspections
            .AsNoTracking()
            .AnyAsync(i => i.TemplateId == id, ct);

        if (inUse)
        {
            entity.IsActive = false;
            await db.SaveChangesAsync(ct);
            return (true, true);
        }

        db.QcChecklistTemplates.Remove(entity);
        await db.SaveChangesAsync(ct);
        return (true, false);
    }
}
