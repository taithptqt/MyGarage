using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public sealed record AdminPartImageUploadResult(
    bool Found,
    long ProductId,
    string? ProductName,
    long? ProductImageId,
    string? ImageUrl,
    bool? IsMain,
    int? SortOrder);

/// <summary>
/// Application use case coordinating product image storage and catalog persistence for admin parts.
/// </summary>
public interface IAdminPartsImageService
{
    Task<AdminPartImageUploadResult> UploadPrimaryImageAsync(
        long productId,
        string contentType,
        byte[] fileBytes,
        CancellationToken ct = default);

    Task<AdminPartImageUploadResult> AppendGalleryImageAsync(
        long productId,
        string contentType,
        byte[] fileBytes,
        CancellationToken ct = default);
}
