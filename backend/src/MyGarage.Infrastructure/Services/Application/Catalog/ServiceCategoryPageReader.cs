using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Catalog;
using MyGarage.Interfaces;

namespace MyGarage.Services;

// Transitional adapter until the legacy EF model and context move to Infrastructure.
public sealed class ServiceCategoryPageReader(IGarageContext db) : IServiceCategoryPageReader
{
    public async Task<ServiceCategoryPage> ReadAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.ServiceCategories.AsNoTracking();
        if (search != null) query = query.Where(category => category.Name.ToLower().Contains(search));
        var total = await query.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total ? new List<ServiceCategoryPageItem>() : await query.OrderBy(category => category.Id)
            .Skip((int)offset).Take(pageSize)
            .Select(x => new ServiceCategoryPageItem(x.Id, x.Code, x.Name, x.ParentCategoryId, x.Description,
                x.IsActive, x.DisplayOrder, x.CreatedAt, x.CreatedBy, x.UpdatedAt, x.UpdatedBy,
                x.IsDeleted, x.DeletedAt, x.DeletedBy)).ToListAsync(cancellationToken);
        return new ServiceCategoryPage(items, total);
    }
}
