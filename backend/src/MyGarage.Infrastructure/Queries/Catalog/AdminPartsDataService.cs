using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfAdminPartsDataService(IGarageContext db) : IAdminPartsDataService
{
    public async Task<(bool Found, long Id, string Name)> GetActiveProductSummaryAsync(long productId, CancellationToken ct = default)
    {
        var product = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == productId && p.IsDeleted != true)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(ct);

        return product is null ? (false, 0, string.Empty) : (true, product.Id, product.Name);
    }

    public async Task SavePrimaryProductImageAsync(long productId, string imageUrl, CancellationToken ct = default)
    {
        var images = await db.ProductImages
            .Where(pi => pi.ProductId == productId)
            .ToListAsync(ct);

        foreach (var image in images.Where(i => i.IsMain == true))
        {
            image.IsMain = false;
        }

        var mainImage = images
            .OrderBy(i => i.SortOrder ?? int.MaxValue)
            .ThenBy(i => i.Id)
            .FirstOrDefault();

        if (mainImage == null)
        {
            mainImage = new ProductImage
            {
                ProductId = productId,
                ImageUrl = imageUrl,
                IsMain = true,
                SortOrder = 1,
            };
            await db.ProductImages.AddAsync(mainImage, ct);
        }
        else
        {
            mainImage.ImageUrl = imageUrl;
            mainImage.IsMain = true;
            if (!mainImage.SortOrder.HasValue || mainImage.SortOrder <= 0)
            {
                mainImage.SortOrder = 1;
            }

            db.ProductImages.Update(mainImage);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<ProductImage> AppendGalleryProductImageAsync(long productId, string imageUrl, CancellationToken ct = default)
    {
        var existing = await db.ProductImages
            .Where(pi => pi.ProductId == productId)
            .ToListAsync(ct);

        var maxOrder = existing.Count == 0
            ? 0
            : existing.Max(i => i.SortOrder ?? 0);

        var isFirst = existing.Count == 0;

        var row = new ProductImage
        {
            ProductId = productId,
            ImageUrl = imageUrl,
            IsMain = isFirst,
            SortOrder = isFirst ? 1 : maxOrder + 1,
        };
        await db.ProductImages.AddAsync(row, ct);
        await db.SaveChangesAsync(ct);
        return row;
    }
}
