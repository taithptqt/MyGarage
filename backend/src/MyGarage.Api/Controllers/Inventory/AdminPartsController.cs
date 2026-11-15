using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Security;

namespace MyGarage.Controllers;

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

    private readonly IAdminPartsDataService _dataService;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly IFileSecurityScanner _fileSecurityScanner;
    private readonly ILogger<AdminPartsController> _logger;

    public AdminPartsController(
        IAdminPartsDataService dataService,
        ICloudStorageService cloudStorageService,
        IFileSecurityScanner fileSecurityScanner,
        ILogger<AdminPartsController> logger)
    {
        _dataService = dataService;
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
            return this.BadRequestProblem(scan.ErrorMessage ?? "Tệp không hợp lệ.");
        }

        var product = await _dataService.GetActiveProductSummaryAsync(id, cancellationToken);
        if (!product.Found)
        {
            return this.NotFoundProblem("Không tìm thấy sản phẩm.");
        }

        try
        {
            var imageUrl = await UploadToBlobAsync(id, file!, scan.Bytes, cancellationToken);
            await _dataService.SavePrimaryProductImageAsync(id, imageUrl, cancellationToken);

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
            return this.ProblemMessage(StatusCodes.Status500InternalServerError, "Không thể tải ảnh lên lúc này.");
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
            return this.BadRequestProblem(scan.ErrorMessage ?? "Tệp không hợp lệ.");
        }

        var product = await _dataService.GetActiveProductSummaryAsync(id, cancellationToken);
        if (!product.Found)
        {
            return this.NotFoundProblem("Không tìm thấy sản phẩm.");
        }

        try
        {
            var imageUrl = await UploadToBlobAsync(id, file!, scan.Bytes, cancellationToken);
            var row = await _dataService.AppendGalleryProductImageAsync(id, imageUrl, cancellationToken);

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
            return this.ProblemMessage(StatusCodes.Status500InternalServerError, "Không thể tải ảnh lên lúc này.");
        }
    }

    private bool TryValidateImageFile(IFormFile? file, out IActionResult? errorResult)
    {
        errorResult = null;
        if (file == null || file.Length <= 0)
        {
            errorResult = this.BadRequestProblem("Tệp ảnh là bắt buộc.");
            return false;
        }

        if (file.Length > MaxUploadBytes)
        {
            errorResult = this.BadRequestProblem("Kích thước ảnh không được vượt quá 5MB.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedMimeTypes.Contains(file.ContentType))
        {
            errorResult = this.BadRequestProblem("Chỉ hỗ trợ ảnh JPEG, PNG hoặc WEBP.");
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
