using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Catalog;
using MyGarage.Interfaces;

namespace MyGarage.Services;

// Transitional adapter until the EF context is moved out of the Web project.
public sealed class StandardServicePageReader(IGarageContext db) : IStandardServicePageReader
{
    public async Task<StandardServicePage> ReadAsync(string? search, bool nameCodeOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var all = db.StandardServices.AsNoTracking().Where(x => x.IsDeleted != true &&
            x.IsActive != false && !x.Code.Trim().ToUpper().StartsWith("PKG-"));
        var totalServices = await all.CountAsync(cancellationToken);
        var activeServices = await all.CountAsync(x => x.IsActive == true, cancellationToken);
        var filtered = all;
        if (search != null)
        {
            var matchesActive = "active".Contains(search);
            var matchesInactive = "inactive".Contains(search);
            filtered = filtered.Where(x => x.Code.ToLower().Contains(search) || x.Name.ToLower().Contains(search) || (!nameCodeOnly &&
                ((x.Category != null && (x.Category.Name != "" ? x.Category.Name : x.Category.Code).ToLower().Contains(search)) ||
                 (x.IsActive == true ? matchesActive : matchesInactive))));
        }
        var total = await filtered.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total ? new List<StandardServicePageItem>() : await filtered.OrderBy(x => x.Id)
            .Skip((int)offset).Take(pageSize)
            .Select(x => new StandardServicePageItem(x.Id, x.Code, x.Name, x.CategoryId, x.BasePrice, x.EngineType,
                x.StandardMinutes, x.TaxRate, x.WarrantyMonths, x.WarrantyKm, x.Description, x.ImageUrl, x.ConsumableCost,
                x.IsActive, x.CreatedAt, x.CreatedBy, x.UpdatedAt, x.UpdatedBy, x.IsDeleted, x.DeletedAt, x.DeletedBy))
            .ToListAsync(cancellationToken);
        var categoryIds = items.Select(x => x.CategoryId).Distinct().ToArray();
        var names = await db.ServiceCategories.AsNoTracking().Where(x => categoryIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name != "" ? x.Name : x.Code, cancellationToken);
        return new StandardServicePage(items, total, totalServices, activeServices, names);
    }
}
