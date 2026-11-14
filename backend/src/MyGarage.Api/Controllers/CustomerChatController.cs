using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.DTOs.Responses.Chat;
using MyGarage.Exceptions;
using MyGarage.Interfaces;
using MyGarage.Security;

namespace MyGarage.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/chat")]
    [Route("api/v1/chat")]
    public class CustomerChatController : ControllerBase
    {
        private const int MaxAttachmentsPerRequest = 6;
        private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;
        private const string SupportedAttachmentHint = "anh, pdf, doc/docx, xls/xlsx, txt/csv, zip/rar";
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
        private readonly IChatV2Service _chatService;
        private readonly ICloudStorageService _cloudStorageService;
        private readonly IFileSecurityScanner _fileSecurityScanner;

        public CustomerChatController(
            IChatV2Service chatService,
            ICloudStorageService cloudStorageService,
            IFileSecurityScanner fileSecurityScanner)
        {
            _chatService = chatService;
            _cloudStorageService = cloudStorageService;
            _fileSecurityScanner = fileSecurityScanner;
        }

        [HttpGet("messenger/conversation")]
        public async Task<ActionResult<ConversationDetailDto>> GetOrCreateMessengerConversation()
        {
            if (!string.Equals(this.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                var conversation = await _chatService.GetOrCreateCustomerMessengerConversationAsync(
                    userId.Value,
                    this.GetCurrentBranchId());
                return Ok(conversation);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("my-conversations")]
        public async Task<ActionResult<PagedConversationsResponseDto>> GetMyConversations(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (!string.Equals(this.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var result = await _chatService.GetCustomerConversationsAsync(userId.Value, status, page, pageSize);
            return Ok(result);
        }

        [HttpGet("conversations/{id:long}")]
        public async Task<ActionResult<ConversationDetailDto>> GetConversation(long id)
        {
            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            if (IsCustomerRole())
            {
                var detail = await _chatService.GetCustomerConversationAsync(userId.Value, id);
                if (detail is null) return NotFound();
                return Ok(detail);
            }

            if (IsStaffRole())
            {
                try
                {
                    var detail = await _chatService.GetReceptionConversationAsync(userId.Value, this.GetCurrentRoleCode(), id);
                    if (detail is null) return NotFound();
                    return Ok(detail);
                }
                catch (UnauthorizedAccessException)
                {
                    return Forbid();
                }
            }

            return Forbid();
        }

        [HttpGet("conversations/{id:long}/messages")]
        public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetMessages(
            long id,
            [FromQuery] long? beforeMessageId = null,
            [FromQuery] int take = 50)
        {
            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                if (IsCustomerRole())
                {
                    var messages = await _chatService.GetCustomerMessagesAsync(userId.Value, id, beforeMessageId, take);
                    return Ok(messages);
                }

                if (IsStaffRole())
                {
                    var messages = await _chatService.GetReceptionMessagesAsync(userId.Value, this.GetCurrentRoleCode(), id, beforeMessageId, take);
                    return Ok(messages);
                }

                return Forbid();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost("conversations")]
        public async Task<ActionResult<ConversationDetailDto>> CreateConversation([FromBody] ChatConversationCreateRequestDto request)
        {
            if (!string.Equals(this.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                var conversation = await _chatService.CreateCustomerConversationAsync(userId.Value, request);
                return CreatedAtAction(nameof(GetConversation), new { id = conversation.Id }, conversation);
            }
            catch (ChatConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("conversations/{id:long}/messages")]
        public async Task<ActionResult<ChatMessageDto>> SendMessage(long id, [FromBody] ChatSendMessageRequestDto request)
        {
            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                ChatMessageDto message;
                if (IsCustomerRole())
                {
                    message = await _chatService.SendCustomerMessageAsync(userId.Value, id, request);
                }
                else if (IsStaffRole())
                {
                    message = await _chatService.SendStaffMessageAsync(userId.Value, this.GetCurrentRoleCode(), id, request);
                }
                else
                {
                    return Forbid();
                }

                return Ok(message);
            }
            catch (ChatConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("conversations/{id:long}/orders")]
        public async Task<ActionResult<IReadOnlyList<ChatOrderCardDto>>> GetConversationOrders(long id)
        {
            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                IReadOnlyList<ChatOrderCardDto> orders;
                if (IsCustomerRole())
                {
                    orders = await _chatService.GetCustomerConversationOrdersAsync(
                        userId.Value,
                        id,
                        HttpContext.RequestAborted);
                }
                else if (IsStaffRole())
                {
                    orders = await _chatService.GetReceptionConversationOrdersAsync(
                        userId.Value,
                        this.GetCurrentRoleCode(),
                        id,
                        HttpContext.RequestAborted);
                }
                else
                {
                    return Forbid();
                }

                return Ok(orders);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost("conversations/{id:long}/attachments")]
        public async Task<ActionResult<IReadOnlyList<ChatUploadAttachmentDto>>> UploadAttachments(
            long id,
            List<IFormFile> files)
        {
            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            if (files is null || files.Count == 0)
            {
            return BadRequest(new { message = "Vui lòng chọn ít nhất 1 tệp đính kèm." });
            }

            if (files.Count > MaxAttachmentsPerRequest)
            {
                return BadRequest(new { message = $"Toi da {MaxAttachmentsPerRequest} tep moi lan gui." });
            }

            try
            {
                if (IsCustomerRole())
                {
                    var conversation = await _chatService.GetCustomerConversationAsync(userId.Value, id, HttpContext.RequestAborted);
                    if (conversation is null)
                    {
                        return NotFound();
                    }
                }
                else if (IsStaffRole())
                {
                    var conversation = await _chatService.GetReceptionConversationAsync(userId.Value, this.GetCurrentRoleCode(), id, HttpContext.RequestAborted);
                    if (conversation is null)
                    {
                        return NotFound();
                    }
                }
                else
                {
                    return Forbid();
                }
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }

            var uploaded = new List<ChatUploadAttachmentDto>(files.Count);
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (file is null || file.Length == 0)
                {
                return BadRequest(new { message = $"Tệp thứ {i + 1} không hợp lệ." });
                }

                if (!TryResolveAttachmentMetadata(file, out var extension, out var normalizedContentType))
                {
                return BadRequest(new { message = $"Tệp thứ {i + 1} không được hỗ trợ. Chỉ hỗ trợ {SupportedAttachmentHint}." });
                }

                if (file.Length > MaxAttachmentSizeBytes)
                {
                return BadRequest(new { message = $"Tệp thứ {i + 1} vượt quá {MaxAttachmentSizeBytes / (1024 * 1024)}MB." });
                }

                var scan = await _fileSecurityScanner.ScanFormFileAsync(
                    file,
                    fileNameOverride: string.IsNullOrWhiteSpace(Path.GetExtension(file.FileName)) ? $"attachment{extension}" : file.FileName,
                    contentTypeOverride: normalizedContentType,
                    maxBytes: MaxAttachmentSizeBytes,
                    cancellationToken: HttpContext.RequestAborted);
                if (!scan.IsClean)
                {
                    return BadRequest(new { message = $"Tệp thứ {i + 1}: {scan.ErrorMessage}" });
                }

                var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
                await using var stream = new MemoryStream(scan.Bytes);
                var attachmentUrl = await _cloudStorageService.UploadAsync(
                    stream,
                    $"chat/conversations/{id}",
                    fileName,
                    normalizedContentType,
                    HttpContext.RequestAborted);

                uploaded.Add(new ChatUploadAttachmentDto
                {
                    AttachmentUrl = attachmentUrl,
                    ContentType = normalizedContentType,
                    FileSize = file.Length,
                    SortOrder = i + 1
                });
            }

            return Ok(uploaded);
        }

        [HttpPut("conversations/{id:long}/read-state")]
        public async Task<ActionResult> MarkRead(long id, [FromBody] ChatUpdateReadStateRequestDto request)
        {
            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                if (IsCustomerRole())
                {
                    await _chatService.MarkCustomerReadAsync(userId.Value, id, request.LastReadMessageId);
                }
                else if (IsStaffRole())
                {
                    await _chatService.MarkStaffReadAsync(userId.Value, this.GetCurrentRoleCode(), id, request.LastReadMessageId);
                }
                else
                {
                    return Forbid();
                }

                return NoContent();
            }
            catch (ChatConflictException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        private static bool TryResolveAttachmentMetadata(IFormFile file, out string extension, out string contentType)
        {
            extension = NormalizeExtension(Path.GetExtension(file.FileName));
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = InferExtensionFromContentType(file.ContentType);
            }

            if (string.IsNullOrWhiteSpace(extension) || !SupportedAttachmentExtensions.Contains(extension))
            {
                contentType = string.Empty;
                return false;
            }

            contentType = NormalizeContentType(file.ContentType, extension);
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

        private bool IsCustomerRole()
        {
            return string.Equals(this.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsStaffRole()
        {
            return string.Equals(this.GetCurrentRole(), "Staff", StringComparison.OrdinalIgnoreCase);
        }
    }
}
