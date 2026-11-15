using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Chat;

namespace MyGarage.Interfaces;

/// <summary>
/// Transport-agnostic representation of an uploaded chat attachment file.
/// </summary>
public sealed record ChatAttachmentUploadInput(
    string? FileName,
    string? ContentType,
    long Length,
    Func<Stream>? OpenReadStream);

/// <summary>
/// Outcome of validating, security-scanning, and uploading chat attachments.
/// </summary>
public sealed record ChatAttachmentUploadResult(
    bool Succeeded,
    IReadOnlyList<ChatUploadAttachmentDto> Attachments,
    string? ErrorMessage)
{
    public static ChatAttachmentUploadResult Ok(IReadOnlyList<ChatUploadAttachmentDto> attachments) =>
        new(true, attachments, null);

    public static ChatAttachmentUploadResult Fail(string errorMessage) =>
        new(false, Array.Empty<ChatUploadAttachmentDto>(), errorMessage);
}

/// <summary>
/// Application service that centralizes chat attachment validation, security scanning, and storage upload.
/// </summary>
public interface IChatAttachmentUploadService
{
    /// <summary>
    /// Validates whether the number of files in an upload batch is within the allowed range.
    /// Returns <c>null</c> when valid, or a Vietnamese validation error message otherwise.
    /// </summary>
    string? ValidateAttachmentCount(int fileCount);

    /// <summary>
    /// Validates, security-scans, and uploads a batch of chat attachments to cloud storage.
    /// </summary>
    Task<ChatAttachmentUploadResult> UploadAsync(
        IReadOnlyList<ChatAttachmentUploadInput?>? files,
        string folderPrefix,
        CancellationToken cancellationToken = default);
}
