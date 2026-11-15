using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Chat;
using MyGarage.Interfaces;

namespace MyGarage.Services;

/// <summary>
/// Centralizes chat attachment count/size limits, extension/MIME resolution, security scanning, and cloud storage upload.
/// </summary>
public sealed class ChatAttachmentUploadService : IChatAttachmentUploadService
{
    public const int MaxAttachmentsPerRequest = 6;
    public const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;
    public const string SupportedAttachmentHint = "anh, pdf, doc/docx, xls/xlsx, txt/csv, zip/rar";

    private static readonly HashSet<string> SupportedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp",
        ".pdf",
        ".doc", ".docx",
        ".xls", ".xlsx",
        ".txt", ".csv",
        ".zip", ".rar"
    };

    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".zip"] = "application/zip",
        [".rar"] = "application/vnd.rar"
    };

    private readonly IFileSecurityScanner _fileSecurityScanner;
    private readonly ICloudStorageService _cloudStorageService;

    public ChatAttachmentUploadService(
        IFileSecurityScanner fileSecurityScanner,
        ICloudStorageService cloudStorageService)
    {
        _fileSecurityScanner = fileSecurityScanner ?? throw new ArgumentNullException(nameof(fileSecurityScanner));
        _cloudStorageService = cloudStorageService ?? throw new ArgumentNullException(nameof(cloudStorageService));
    }

    /// <inheritdoc />
    public string? ValidateAttachmentCount(int fileCount)
    {
        if (fileCount <= 0)
        {
            return "Vui lòng chọn ít nhất 1 tệp đính kèm.";
        }

        if (fileCount > MaxAttachmentsPerRequest)
        {
            return $"Toi da {MaxAttachmentsPerRequest} tep moi lan gui.";
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<ChatAttachmentUploadResult> UploadAsync(
        IReadOnlyList<ChatAttachmentUploadInput?>? files,
        string folderPrefix,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPrefix);
        var normalizedFolder = folderPrefix.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(normalizedFolder))
        {
            throw new ArgumentException("Folder prefix cannot be empty.", nameof(folderPrefix));
        }

        var countError = ValidateAttachmentCount(files?.Count ?? 0);
        if (countError is not null)
        {
            return ChatAttachmentUploadResult.Fail(countError);
        }

        // Validate and security-scan the entire batch before uploading so partial failures leave no orphan blobs.
        var prepared = new List<PreparedAttachment>(files!.Count);
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file is null || file.Length <= 0 || file.OpenReadStream is null)
            {
                return ChatAttachmentUploadResult.Fail($"Tệp thứ {i + 1} không hợp lệ.");
            }

            if (!TryResolveAttachmentMetadata(file.FileName, file.ContentType, out var extension, out var normalizedContentType))
            {
                return ChatAttachmentUploadResult.Fail(
                    $"Tệp thứ {i + 1} không được hỗ trợ. Chỉ hỗ trợ {SupportedAttachmentHint}.");
            }

            if (file.Length > MaxAttachmentSizeBytes)
            {
                return ChatAttachmentUploadResult.Fail(
                    $"Tệp thứ {i + 1} vượt quá {MaxAttachmentSizeBytes / (1024 * 1024)}MB.");
            }

            await using var sourceStream = file.OpenReadStream();
            if (sourceStream is null)
            {
                return ChatAttachmentUploadResult.Fail($"Tệp thứ {i + 1} không hợp lệ.");
            }

            var (bytes, isEmpty, exceedsLimit) = await ReadStreamBoundedAsync(
                sourceStream,
                MaxAttachmentSizeBytes,
                cancellationToken);

            if (isEmpty)
            {
                return ChatAttachmentUploadResult.Fail($"Tệp thứ {i + 1} không hợp lệ.");
            }

            if (exceedsLimit)
            {
                return ChatAttachmentUploadResult.Fail(
                    $"Tệp thứ {i + 1} vượt quá {MaxAttachmentSizeBytes / (1024 * 1024)}MB.");
            }

            // Preserve inferred extension when client uploads extensionless filenames (e.g. clipboard blobs).
            var scanFileName = string.IsNullOrWhiteSpace(Path.GetExtension(file.FileName))
                ? $"attachment{extension}"
                : file.FileName!;

            var scanResult = await _fileSecurityScanner.ScanAsync(
                new FileSecurityScanRequest
                {
                    FileName = scanFileName,
                    ContentType = normalizedContentType,
                    FileBytes = bytes
                },
                cancellationToken);

            if (!scanResult.IsClean)
            {
                var errorMessage = string.IsNullOrWhiteSpace(scanResult.DetectionMessage)
                    ? "Tệp tải lên không an toàn."
                    : scanResult.DetectionMessage;
                return ChatAttachmentUploadResult.Fail($"Tệp thứ {i + 1}: {errorMessage}");
            }

            prepared.Add(new PreparedAttachment(bytes, extension, normalizedContentType));
        }

        var uploaded = new List<ChatUploadAttachmentDto>(prepared.Count);
        for (var i = 0; i < prepared.Count; i++)
        {
            var item = prepared[i];
            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{item.Extension}";
            await using var stream = new MemoryStream(item.Bytes, writable: false);
            var attachmentUrl = await _cloudStorageService.UploadAsync(
                stream,
                normalizedFolder,
                fileName,
                item.ContentType,
                cancellationToken);

            uploaded.Add(new ChatUploadAttachmentDto
            {
                AttachmentUrl = attachmentUrl,
                ContentType = item.ContentType,
                FileSize = item.Bytes.LongLength,
                SortOrder = i + 1
            });
        }

        return ChatAttachmentUploadResult.Ok(uploaded);
    }

    private static async Task<(byte[] Bytes, bool IsEmpty, bool ExceedsLimit)> ReadStreamBoundedAsync(
        Stream stream,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var memoryStream = new MemoryStream();
        var buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            if (memoryStream.Length + bytesRead > maxBytes)
            {
                return (Array.Empty<byte>(), false, true);
            }

            memoryStream.Write(buffer, 0, bytesRead);
        }

        if (memoryStream.Length == 0)
        {
            return (Array.Empty<byte>(), true, false);
        }

        return (memoryStream.ToArray(), false, false);
    }

    private static bool TryResolveAttachmentMetadata(
        string? fileName,
        string? rawContentType,
        out string extension,
        out string contentType)
    {
        extension = NormalizeExtension(Path.GetExtension(fileName));
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = InferExtensionFromContentType(rawContentType);
        }

        if (string.IsNullOrWhiteSpace(extension) || !SupportedAttachmentExtensions.Contains(extension))
        {
            contentType = string.Empty;
            return false;
        }

        contentType = NormalizeContentType(rawContentType, extension);
        return true;
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.Trim().ToLowerInvariant();
        return normalized.StartsWith(".", StringComparison.Ordinal) ? normalized : $".{normalized}";
    }

    private static string InferExtensionFromContentType(string? contentType)
    {
        return (contentType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "application/pdf" => ".pdf",
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "text/plain" => ".txt",
            "text/csv" => ".csv",
            "application/zip" => ".zip",
            "application/x-zip-compressed" => ".zip",
            "application/vnd.rar" => ".rar",
            "application/x-rar-compressed" => ".rar",
            _ => string.Empty
        };
    }

    private static string NormalizeContentType(string? contentType, string extension)
    {
        var normalized = (contentType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || string.Equals(normalized, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            if (ContentTypeByExtension.TryGetValue(extension, out var mapped))
            {
                return mapped;
            }

            return "application/octet-stream";
        }

        return normalized;
    }

    private sealed record PreparedAttachment(byte[] Bytes, string Extension, string ContentType);
}
