using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class ChatbotImageUploadService : IChatbotImageUploadService
{
    private const long MaxFileBytes = 8 * 1024 * 1024;
    private const string UploadActionType = "UPLOAD_IMAGE_ANALYSIS";
    private const string DefaultUserMessage = "Uploaded an image for AI review.";
    private const string PreliminaryDisclaimer =
        "Preliminary AI assessment only. Please confirm with an in-person inspection by a MyGarage technician.";

    private static readonly IReadOnlyDictionary<string, string> MimeTypeByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    private readonly IGarageContext _context;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly IVisionAnalyzer _visionAnalyzer;
    private readonly IFileSecurityScanner _fileSecurityScanner;
    private readonly ISystemClock _clock;
    private readonly ILogger<ChatbotImageUploadService> _logger;

    public ChatbotImageUploadService(
        IGarageContext context,
        ICloudStorageService cloudStorageService,
        IVisionAnalyzer visionAnalyzer,
        IFileSecurityScanner fileSecurityScanner,
        ISystemClock clock,
        ILogger<ChatbotImageUploadService> logger)
    {
        _context = context;
        _cloudStorageService = cloudStorageService;
        _visionAnalyzer = visionAnalyzer;
        _fileSecurityScanner = fileSecurityScanner;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ChatbotImageUploadResponseDto> UploadAndAnalyzeAsync(
        long customerId,
        UploadChatbotImageInput request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request body is required.");
        }

        if (request.Content is null)
        {
            throw new InvalidOperationException("File is required.");
        }

        if (request.Length <= 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        await EnsureActiveCustomerAsync(customerId, cancellationToken);

        var session = await ResolveOwnedSessionAsync(customerId, request, cancellationToken)
            ?? throw new KeyNotFoundException("Chatbot session not found.");

        if (IsLockedSession(session.Status))
        {
            throw new InvalidOperationException("Session is locked and cannot accept new uploads.");
        }

        var extension = ResolveExtension(request.FileName, request.ContentType);
        if (string.IsNullOrWhiteSpace(extension) || !MimeTypeByExtension.ContainsKey(extension))
        {
            throw new InvalidOperationException("Unsupported image type. Allowed types: .jpg, .jpeg, .png, .webp.");
        }

        var normalizedContentType = NormalizeAndValidateContentType(request.ContentType, extension);
        var imageBytes = await ReadFileBytesAsync(request.Content, cancellationToken);
        ValidateFileSignature(imageBytes, extension);

        var scanResult = await _fileSecurityScanner.ScanAsync(
            new FileSecurityScanRequest
            {
                FileName = request.FileName ?? string.Empty,
                ContentType = normalizedContentType,
                FileBytes = imageBytes
            },
            cancellationToken);

        if (!scanResult.IsClean)
        {
            await TryLogFailureActionAsync(
                session.Id,
                "UPLOAD_BLOCKED_BY_SCANNER",
                scanResult.DetectionCode ?? "MALWARE_SCAN_BLOCK",
                scanResult.DetectionMessage ?? "Upload blocked by security scanner.",
                cancellationToken);

            throw new InvalidOperationException("Upload blocked by file security scanner.");
        }

        var storageFileName = $"{_clock.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
        string attachmentUrl;
        try
        {
            await using var stream = new MemoryStream(imageBytes, writable: false);
            attachmentUrl = await _cloudStorageService.UploadAsync(
                stream,
                $"chatbot/sessions/{session.Id}",
                storageFileName,
                normalizedContentType,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Chatbot image storage failed. SessionId={SessionId}, CustomerId={CustomerId}",
                session.Id,
                customerId);

            await TryLogFailureActionAsync(
                session.Id,
                "UPLOAD_STORAGE_FAILED",
                "STORAGE_UPLOAD_FAILED",
                ex.Message,
                cancellationToken);

            throw new InvalidOperationException("Failed to store uploaded image.");
        }

        VisionAnalyzerResult analysis;
        try
        {
            analysis = await _visionAnalyzer.AnalyzeAsync(
                new VisionAnalyzerRequest
                {
                    FileName = request.FileName ?? storageFileName,
                    ContentType = normalizedContentType,
                    ImageBytes = imageBytes,
                    AttachmentUrl = attachmentUrl,
                    UserNote = request.Note?.Trim()
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Chatbot image analysis failed. SessionId={SessionId}, CustomerId={CustomerId}",
                session.Id,
                customerId);

            await TryLogFailureActionAsync(
                session.Id,
                "UPLOAD_ANALYSIS_FAILED",
                "VISION_ANALYSIS_FAILED",
                ex.Message,
                cancellationToken);

            throw new InvalidOperationException("Failed to analyze uploaded image.");
        }

        var normalizedAnalysis = NormalizeAnalysis(analysis);
        var now = VietnamTime.Now;
        var userMessage = new ChatbotMessage
        {
            SessionId = session.Id,
            Role = nameof(ChatbotMessageRole.USER),
            Content = string.IsNullOrWhiteSpace(request.Note) ? DefaultUserMessage : request.Note.Trim(),
            IntentName = "IMAGE_UPLOAD",
            AttachmentUrl = attachmentUrl,
            CreatedAt = now
        };

        var assistantMessage = new ChatbotMessage
        {
            SessionId = session.Id,
            Role = nameof(ChatbotMessageRole.ASSISTANT),
            Content = BuildAssistantMessage(normalizedAnalysis),
            IntentName = "IMAGE_ANALYSIS",
            CreatedAt = now
        };

        var strategy = _context.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(cancellationToken);
            try
            {
                _context.ChatbotMessages.Add(userMessage);
                _context.ChatbotMessages.Add(assistantMessage);

                _context.ChatbotActionLogs.Add(new ChatbotActionLog
                {
                    SessionId = session.Id,
                    ActionType = UploadActionType,
                    Status = nameof(ChatbotActionStatus.SUCCESS),
                    ActionPayload = JsonSerializer.Serialize(new
                    {
                        attachment_url = attachmentUrl,
                        suspected_category = normalizedAnalysis.SuspectedCategory,
                        suggested_service_categories = normalizedAnalysis.SuggestedServiceCategories
                    }),
                    CreatedAt = now
                });

                session.LastActivityAt = now;
                if (session.Status == nameof(ChatbotSessionStatus.IDLE))
                {
                    session.Status = nameof(ChatbotSessionStatus.ACTIVE);
                    session.CurrentIntent = "IMAGE_ANALYSIS";
                }

                await _context.SaveChangesAsync(cancellationToken);
                if (tx != null) await tx.CommitAsync();

                return new ChatbotImageUploadResponseDto
                {
                    SessionId = session.Id,
                    AttachmentUrl = attachmentUrl,
                    UserMessageId = userMessage.Id,
                    AssistantMessageId = assistantMessage.Id,
                    Analysis = new ChatbotVisionAnalysisResponseDto
                    {
                        ImageSummary = normalizedAnalysis.ImageSummary,
                        SuspectedCategory = normalizedAnalysis.SuspectedCategory,
                        SuggestedServiceCategories = normalizedAnalysis.SuggestedServiceCategories,
                        Disclaimer = PreliminaryDisclaimer
                    }
                };
            }
            catch (Exception ex)
            {
                if (tx != null) await tx.RollbackAsync();

                _logger.LogError(
                    ex,
                    "Chatbot image upload DB persistence failed. SessionId={SessionId}, CustomerId={CustomerId}",
                    session.Id,
                    customerId);

                throw;
            }
        });

        _logger.LogInformation(
            "Chatbot image uploaded and analyzed. SessionId={SessionId}, CustomerId={CustomerId}, UserMessageId={UserMessageId}, AssistantMessageId={AssistantMessageId}",
            session.Id,
            customerId,
            userMessage.Id,
            assistantMessage.Id);

        return result;
    }

    private async Task EnsureActiveCustomerAsync(long customerId, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers.AsNoTracking()
            .Where(x => x.Id == customerId)
            .Select(x => new { x.Id, x.IsDeleted, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null || customer.IsDeleted == true)
        {
            throw new KeyNotFoundException("Customer not found.");
        }

        if (customer.IsActive == false)
        {
            throw new InvalidOperationException("Customer is inactive.");
        }
    }

    private async Task<ChatbotSession?> ResolveOwnedSessionAsync(
        long customerId,
        UploadChatbotImageInput request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId.GetValueOrDefault() > 0)
        {
            return await _context.ChatbotSessions
                .FirstOrDefaultAsync(
                    session => session.Id == request.SessionId!.Value && session.CustomerId == customerId,
                    cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.SessionToken))
        {
            var token = request.SessionToken.Trim();
            return await _context.ChatbotSessions
                .FirstOrDefaultAsync(
                    session => session.SessionToken == token && session.CustomerId == customerId,
                    cancellationToken);
        }

        throw new InvalidOperationException("SessionId or SessionToken is required.");
    }

    private static bool IsLockedSession(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var normalized = status.Trim().ToUpperInvariant();
        return normalized is nameof(ChatbotSessionStatus.HANDED_OFF)
            or nameof(ChatbotSessionStatus.EXPIRED)
            or nameof(ChatbotSessionStatus.CLOSED)
            or nameof(ChatbotSessionStatus.ARCHIVED);
    }

    private static string ResolveExtension(string? fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName)?.Trim().ToLowerInvariant() ?? string.Empty;
        if (MimeTypeByExtension.ContainsKey(extension))
        {
            return extension;
        }

        return (contentType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => string.Empty
        };
    }

    private static string NormalizeAndValidateContentType(string? contentType, string extension)
    {
        if (!MimeTypeByExtension.TryGetValue(extension, out var expectedMime))
        {
            throw new InvalidOperationException("Unsupported image content type.");
        }

        var normalized = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Content-Type is required for image upload.");
        }

        var isValid = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? normalized is "image/jpeg" or "image/pjpeg"
            : string.Equals(normalized, expectedMime, StringComparison.OrdinalIgnoreCase);

        if (!isValid)
        {
            throw new InvalidOperationException("Uploaded file Content-Type does not match file type.");
        }

        return expectedMime;
    }

    private static async Task<byte[]> ReadFileBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            memory.Write(buffer, 0, read);
            if (memory.Length > MaxFileBytes)
            {
                throw new InvalidOperationException($"Uploaded file exceeds size limit of {MaxFileBytes / (1024 * 1024)} MB.");
            }
        }

        if (memory.Length <= 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        return memory.ToArray();
    }

    private static void ValidateFileSignature(byte[] bytes, string extension)
    {
        var isValid = extension switch
        {
            ".jpg" or ".jpeg" => IsJpeg(bytes),
            ".png" => IsPng(bytes),
            ".webp" => IsWebp(bytes),
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidOperationException("Uploaded file signature is invalid or does not match the file type.");
        }
    }

    private static bool IsJpeg(byte[] data)
    {
        return data.Length >= 3
               && data[0] == 0xFF
               && data[1] == 0xD8
               && data[2] == 0xFF;
    }

    private static bool IsPng(byte[] data)
    {
        return data.Length >= 8
               && data[0] == 0x89
               && data[1] == 0x50
               && data[2] == 0x4E
               && data[3] == 0x47
               && data[4] == 0x0D
               && data[5] == 0x0A
               && data[6] == 0x1A
               && data[7] == 0x0A;
    }

    private static bool IsWebp(byte[] data)
    {
        return data.Length >= 12
               && data[0] == 0x52
               && data[1] == 0x49
               && data[2] == 0x46
               && data[3] == 0x46
               && data[8] == 0x57
               && data[9] == 0x45
               && data[10] == 0x42
               && data[11] == 0x50;
    }

    private static ChatbotVisionAnalysisResponseDto NormalizeAnalysis(VisionAnalyzerResult? result)
    {
        var safeSuggestions = result?.SuggestedServiceCategories?
                                  .Where(item => !string.IsNullOrWhiteSpace(item))
                                  .Select(item => item.Trim())
                                  .Distinct(StringComparer.OrdinalIgnoreCase)
                                  .ToArray()
                              ?? [];

        return new ChatbotVisionAnalysisResponseDto
        {
            ImageSummary = string.IsNullOrWhiteSpace(result?.ImageSummary)
                ? "Image received successfully. Analysis details are limited."
                : result.ImageSummary.Trim(),
            SuspectedCategory = string.IsNullOrWhiteSpace(result?.SuspectedCategory)
                ? "UNKNOWN"
                : result.SuspectedCategory.Trim(),
            SuggestedServiceCategories = safeSuggestions.Length == 0 ? ["General Inspection"] : safeSuggestions,
            Disclaimer = PreliminaryDisclaimer
        };
    }

    private static string BuildAssistantMessage(ChatbotVisionAnalysisResponseDto analysis)
    {
        var services = analysis.SuggestedServiceCategories.Count == 0
            ? "General Inspection"
            : string.Join(", ", analysis.SuggestedServiceCategories);

        return $"Image summary: {analysis.ImageSummary}\n"
               + $"Suspected category: {analysis.SuspectedCategory}\n"
               + $"Suggested services: {services}\n"
               + $"Disclaimer: {analysis.Disclaimer}";
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _context.Database.BeginTransactionAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task TryLogFailureActionAsync(
        long sessionId,
        string actionType,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            _context.ChatbotActionLogs.Add(new ChatbotActionLog
            {
                SessionId = sessionId,
                ActionType = actionType,
                Status = nameof(ChatbotActionStatus.FAILED),
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                CreatedAt = _clock.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist chatbot image failure log. SessionId={SessionId}, ActionType={ActionType}",
                sessionId,
                actionType);
        }
    }
}
