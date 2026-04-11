using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers;

[Route("api/admin")]
[ApiController]
[Authorize(Policy = "AdminStaffOnly")]
public class AdminPartsController : ControllerBase
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    private readonly IGarageContext _context;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly ILogger<AdminPartsController> _logger;

    public AdminPartsController(
        IGarageContext context,
        ICloudStorageService cloudStorageService,
        ILogger<AdminPartsController> logger)
    {
        _context = context;
        _cloudStorageService = cloudStorageService;
        _logger = logger;
    }

    [HttpPost("parts/{id:long}/image")]
    [HttpPost("products/{id:long}/image")]
    public async Task<IActionResult> UploadProductImage(long id, [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length <= 0)
        {
            return BadRequest(new { message = "Tệp ảnh là bắt buộc." });
        }

        if (file.Length > MaxUploadBytes)
        {
            return BadRequest(new { message = "Kích thước ảnh không được vượt quá 5MB." });
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedMimeTypes.Contains(file.ContentType))
        {
            return BadRequest(new { message = "Chỉ hỗ trợ ảnh JPEG, PNG hoặc WEBP." });
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted != true, cancellationToken);
        if (product == null)
        {
            return NotFound(new { message = "Không tìm thấy sản phẩm." });
        }

        try
        {
            var extension = ResolveExtension(file.ContentType);
            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
            var folderName = $"products/{id}";

            await using var stream = file.OpenReadStream();
            var imageUrl = await _cloudStorageService.UploadAsync(
                stream,
                folderName,
                fileName,
                file.ContentType,
                cancellationToken);

            var images = await _context.ProductImages
                .Where(pi => pi.ProductId == id)
                .ToListAsync(cancellationToken);

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
                    ProductId = id,
                    ImageUrl = imageUrl,
                    IsMain = true,
                    SortOrder = 1,
                };
                await _context.ProductImages.AddAsync(mainImage, cancellationToken);
            }
            else
            {
                mainImage.ImageUrl = imageUrl;
                mainImage.IsMain = true;
                if (!mainImage.SortOrder.HasValue || mainImage.SortOrder <= 0)
                {
                    mainImage.SortOrder = 1;
                }
                _context.ProductImages.Update(mainImage);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                id = product.Id,
                name = product.Name,
                imageUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload image for product {ProductId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể tải ảnh lên lúc này."
            });
        }
    }

    private static string ResolveExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin",
        };
}
