using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Security;

namespace MyGarage.Controllers;

[Route("api/admin")]
[Route("api/v1/admin")]
[ApiController]
[Authorize(Policy = "AdminStaffOnly")]
public class AdminPartsController : ControllerBase
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/pjpeg",
        "image/png",
        "image/webp",
    };

    private readonly IGarageContext _context;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly IFileSecurityScanner _fileSecurityScanner;
    private readonly ILogger<AdminPartsController> _logger;

    public AdminPartsController(
        IGarageContext context,
        ICloudStorageService cloudStorageService,
        IFileSecurityScanner fileSecurityScanner,
        ILogger<AdminPartsController> logger)
    {
        _context = context;
        _cloudStorageService = cloudStorageService;
        _fileSecurityScanner = fileSecurityScanner;
        _logger = logger;
    }

    /// <summary>
    /// Upload ảnh đại diện: ghi đè bản ghi ảnh đầu tiên (theo SortOrder) hoặc tạo mới nếu chưa có.
    /// URL lưu trong ProductImages.ImageUrl (blob Azure).
    /// </summary>
    [HttpPost("parts/{id:long}/image")]
    [HttpPost("products/{id:long}/image")]
    public Task<IActionResult> UploadProductImage(long id, IFormFile? file, CancellationToken cancellationToken) =>
        UploadPrimaryProductImageAsync(id, file, cancellationToken);

    /// <summary>
    /// Thêm một ảnh phụ vào gallery (luôn tạo dòng mới). Ảnh đầu tiên của sản phẩm được đánh dấu IsMain.
    /// </summary>
    [HttpPost("parts/{id:long}/gallery-image")]
    [HttpPost("products/{id:long}/gallery-image")]
    public Task<IActionResult> AppendProductGalleryImage(long id, IFormFile? file, CancellationToken cancellationToken) =>
        AppendGalleryImageAsync(id, file, cancellationToken);

    private async Task<IActionResult> UploadPrimaryProductImageAsync(long id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (!TryValidateImageFile(file, out var badRequest))
        {
            return badRequest!;
        }

        var scan = await _fileSecurityScanner.ScanFormFileAsync(
            file,
            fileNameOverride: string.IsNullOrWhiteSpace(Path.GetExtension(file!.FileName))
                ? $"part{ResolveExtension(file.ContentType)}"
                : file.FileName,
            maxBytes: MaxUploadBytes,
            cancellationToken: cancellationToken);
        if (!scan.IsClean)
        {
            return BadRequest(new { message = scan.ErrorMessage });
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted != true, cancellationToken);
        if (product == null)
        {
            return NotFound(new { message = "Không tìm thấy sản phẩm." });
        }

        try
        {
            var imageUrl = await UploadToBlobAsync(id, file!, scan.Bytes, cancellationToken);

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

    private async Task<IActionResult> AppendGalleryImageAsync(long id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (!TryValidateImageFile(file, out var badRequest))
        {
            return badRequest!;
        }

        var scan = await _fileSecurityScanner.ScanFormFileAsync(
            file,
            fileNameOverride: string.IsNullOrWhiteSpace(Path.GetExtension(file!.FileName))
                ? $"part{ResolveExtension(file.ContentType)}"
                : file.FileName,
            maxBytes: MaxUploadBytes,
            cancellationToken: cancellationToken);
        if (!scan.IsClean)
        {
            return BadRequest(new { message = scan.ErrorMessage });
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted != true, cancellationToken);
        if (product == null)
        {
            return NotFound(new { message = "Không tìm thấy sản phẩm." });
        }

        try
        {
            var imageUrl = await UploadToBlobAsync(id, file!, scan.Bytes, cancellationToken);

            var existing = await _context.ProductImages
                .Where(pi => pi.ProductId == id)
                .ToListAsync(cancellationToken);

            var maxOrder = existing.Count == 0
                ? 0
                : existing.Max(i => i.SortOrder ?? 0);

            var isFirst = existing.Count == 0;

            var row = new ProductImage
            {
                ProductId = id,
                ImageUrl = imageUrl,
                IsMain = isFirst,
                SortOrder = isFirst ? 1 : maxOrder + 1,
            };
            await _context.ProductImages.AddAsync(row, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                id = row.Id,
                productId = id,
                imageUrl,
                isMain = row.IsMain,
                sortOrder = row.SortOrder
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append gallery image for product {ProductId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể tải ảnh lên lúc này."
            });
        }
    }

    private bool TryValidateImageFile(IFormFile? file, out IActionResult? errorResult)
    {
        errorResult = null;
        if (file == null || file.Length <= 0)
        {
            errorResult = BadRequest(new { message = "Tệp ảnh là bắt buộc." });
            return false;
        }

        if (file.Length > MaxUploadBytes)
        {
            errorResult = BadRequest(new { message = "Kích thước ảnh không được vượt quá 5MB." });
            return false;
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedMimeTypes.Contains(file.ContentType))
        {
            errorResult = BadRequest(new { message = "Chỉ hỗ trợ ảnh JPEG, PNG hoặc WEBP." });
            return false;
        }

        return true;
    }

    private async Task<string> UploadToBlobAsync(long productId, IFormFile file, byte[] fileBytes, CancellationToken cancellationToken)
    {
        var extension = ResolveExtension(file.ContentType);
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
        var folderName = $"products/{productId}";

        await using var stream = new MemoryStream(fileBytes);
        return await _cloudStorageService.UploadAsync(
            stream,
            folderName,
            fileName,
            file.ContentType,
            cancellationToken);
    }

    private static string ResolveExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" or "image/pjpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin",
        };
}
