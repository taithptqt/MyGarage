namespace MyGarage.Interfaces;

public interface ICloudStorageService
{
    Task<string> UploadAsync(Stream stream, string folderName, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default);
}
