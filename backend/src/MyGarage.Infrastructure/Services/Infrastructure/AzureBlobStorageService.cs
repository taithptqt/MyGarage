using System;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class AzureBlobStorageService : ICloudStorageService
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureStorage:ConnectionString"];
        var containerName = configuration["AzureStorage:ContainerName"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Azure storage connection string is missing.");
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            containerName = "uploads";
        }

        _containerClient = new BlobContainerClient(connectionString, containerName);
    }

    public async Task<string> UploadAsync(
        Stream stream,
        string folderName,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var safeFolder = string.IsNullOrWhiteSpace(folderName)
            ? string.Empty
            : folderName.Trim('/').Replace("\\", "/");

        var safeFileName = fileName.Replace("\\", "/").Trim('/');
        var blobPath = string.IsNullOrWhiteSpace(safeFolder) ? safeFileName : $"{safeFolder}/{safeFileName}";

        var blobClient = _containerClient.GetBlobClient(blobPath);

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
            }
        };

        await blobClient.UploadAsync(stream, options, cancellationToken);
        return blobClient.Uri.ToString();
    }

    public async Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            return;
        }

        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        var normalizedBlobPath = blobPath.Trim('/').Replace("\\", "/");
        var blobClient = _containerClient.GetBlobClient(normalizedBlobPath);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
