using Microsoft.AspNetCore.Http;
using MyGarage.Interfaces;

namespace MyGarage.Security;

public static class FileUploadSecurityHelper
{
    public const long DefaultMaxUploadBytes = 10 * 1024 * 1024;

    public static async Task<(bool IsClean, string? ErrorMessage, byte[] Bytes)> ScanFormFileAsync(
        this IFileSecurityScanner scanner,
        IFormFile? file,
        string? fileNameOverride = null,
        string? contentTypeOverride = null,
        long maxBytes = DefaultMaxUploadBytes,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length <= 0)
        {
            return (false, "Tệp tải lên không hợp lệ hoặc rỗng.", Array.Empty<byte>());
        }

        if (file.Length > maxBytes)
        {
            return (false, $"Tệp vượt quá dung lượng tối đa {maxBytes / (1024 * 1024)}MB.", Array.Empty<byte>());
        }

        await using var memoryStream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        var scanResult = await scanner.ScanAsync(new FileSecurityScanRequest
        {
            FileName = string.IsNullOrWhiteSpace(fileNameOverride) ? file.FileName : fileNameOverride,
            ContentType = string.IsNullOrWhiteSpace(contentTypeOverride) ? file.ContentType : contentTypeOverride,
            FileBytes = bytes
        }, cancellationToken);

        return scanResult.IsClean
            ? (true, null, bytes)
            : (false, scanResult.DetectionMessage ?? "Tệp tải lên không an toàn.", Array.Empty<byte>());
    }

    public static IReadOnlyList<ChatAttachmentUploadInput?> ToChatAttachmentInputs(
        this IReadOnlyList<IFormFile>? files)
    {
        if (files is null || files.Count == 0)
        {
            return Array.Empty<ChatAttachmentUploadInput?>();
        }

        var inputs = new ChatAttachmentUploadInput?[files.Count];
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            inputs[i] = file is null
                ? null
                : new ChatAttachmentUploadInput(
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    file.OpenReadStream);
        }

        return inputs;
    }
}
