using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Services;

/// <summary>
/// Coordinates admin product image storage and DB persistence without coupling API controllers to cloud storage.
/// </summary>
public sealed class AdminPartsImageService : IAdminPartsImageService
{
    private readonly IAdminPartsDataService _dataService;
    private readonly ICloudStorageService _cloudStorageService;

    public AdminPartsImageService(
        IAdminPartsDataService dataService,
        ICloudStorageService cloudStorageService)
    {
        _dataService = dataService;
        _cloudStorageService = cloudStorageService;
    }

    public async Task<AdminPartImageUploadResult> UploadPrimaryImageAsync(
        long productId,
        string contentType,
        byte[] fileBytes,
        CancellationToken ct = default)
    {
        var product = await _dataService.GetActiveProductSummaryAsync(productId, ct);
        if (!product.Found)
        {
            return new AdminPartImageUploadResult(false, productId, null, null, null, null, null);
        }

        var imageUrl = await UploadToBlobAsync(productId, contentType, fileBytes, ct);
        await _dataService.SavePrimaryProductImageAsync(productId, imageUrl, ct);
        return new AdminPartImageUploadResult(true, product.Id, product.Name, null, imageUrl, true, 1);
    }

    public async Task<AdminPartImageUploadResult> AppendGalleryImageAsync(
        long productId,
        string contentType,
        byte[] fileBytes,
        CancellationToken ct = default)
    {
        var product = await _dataService.GetActiveProductSummaryAsync(productId, ct);
        if (!product.Found)
        {
            return new AdminPartImageUploadResult(false, productId, null, null, null, null, null);
        }

        var imageUrl = await UploadToBlobAsync(productId, contentType, fileBytes, ct);
        var row = await _dataService.AppendGalleryProductImageAsync(productId, imageUrl, ct);
        return new AdminPartImageUploadResult(true, product.Id, product.Name, row.Id, imageUrl, row.IsMain, row.SortOrder);
    }

    private async Task<string> UploadToBlobAsync(long productId, string contentType, byte[] fileBytes, CancellationToken ct)
    {
        var extension = ResolveExtension(contentType);
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
        var folderName = $"products/{productId}";

        await using var stream = new MemoryStream(fileBytes);
        return await _cloudStorageService.UploadAsync(
            stream,
            folderName,
            fileName,
            contentType,
            ct);
    }

    private static string ResolveExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" or "image/pjpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin"
        };
}
