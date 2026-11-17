using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    private readonly IAdminPartsImageService _imageService;
    private readonly IFileSecurityScanner _fileSecurityScanner;
    private readonly ILogger<AdminPartsController> _logger;

    public AdminPartsController(
        IAdminPartsImageService imageService,
        IFileSecurityScanner fileSecurityScanner,
        ILogger<AdminPartsController> logger)
    {
        _imageService = imageService;
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
        UploadImageAsync(id, file, isGallery: false, cancellationToken);

    /// <summary>
    /// Thêm một ảnh phụ vào gallery (luôn tạo dòng mới). Ảnh đầu tiên của sản phẩm được đánh dấu IsMain.
    /// </summary>
    [HttpPost("parts/{id:long}/gallery-image")]
    [HttpPost("products/{id:long}/gallery-image")]
    [HttpPost("products/{id:long}/images")]
    public Task<IActionResult> AppendProductGalleryImage(long id, IFormFile? file, CancellationToken cancellationToken) =>
        UploadImageAsync(id, file, isGallery: true, cancellationToken);

    private async Task<IActionResult> UploadImageAsync(
        long id,
        IFormFile? file,
        bool isGallery,
        CancellationToken cancellationToken)
    {
        var (bytes, errorResult) = await ValidateAndScanImageAsync(file, cancellationToken);
        if (errorResult != null)
        {
            return errorResult;
        }

        try
        {
            var result = isGallery
                ? await _imageService.AppendGalleryImageAsync(id, file!.ContentType, bytes!, cancellationToken)
                : await _imageService.UploadPrimaryImageAsync(id, file!.ContentType, bytes!, cancellationToken);

            if (!result.Found)
            {
                return this.NotFoundProblem("Không tìm thấy sản phẩm.");
            }

            if (isGallery)
            {
                return Ok(new
                {
                    id = result.ProductImageId,
                    productId = result.ProductId,
                    imageUrl = result.ImageUrl,
                    isMain = result.IsMain,
                    sortOrder = result.SortOrder
                });
            }

            return Ok(new
            {
                id = result.ProductId,
                name = result.ProductName,
                imageUrl = result.ImageUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload image for product {ProductId} (isGallery={IsGallery})", id, isGallery);
            return this.ProblemMessage(StatusCodes.Status500InternalServerError, "Không thể tải ảnh lên lúc này.");
        }
    }

    private async Task<(byte[]? Bytes, IActionResult? ErrorResult)> ValidateAndScanImageAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!TryValidateImageFile(file, out var badRequest))
        {
            return (null, badRequest);
        }

        var scan = await _fileSecurityScanner.ScanFormFileAsync(
            file!,
            fileNameOverride: string.IsNullOrWhiteSpace(Path.GetExtension(file!.FileName))
                ? $"part{ResolveExtension(file.ContentType)}"
                : file.FileName,
            maxBytes: MaxUploadBytes,
            cancellationToken: cancellationToken);
        if (!scan.IsClean)
        {
            return (null, this.BadRequestProblem(scan.ErrorMessage ?? "Tệp không hợp lệ."));
        }

        return (scan.Bytes, null);
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

    private static string ResolveExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" or "image/pjpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin",
        };
}
