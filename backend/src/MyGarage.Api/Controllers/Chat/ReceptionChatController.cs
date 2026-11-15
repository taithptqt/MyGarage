using System;
using System.Collections.Generic;
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
    [Route("api/v1/reception/chat")]
    public class ReceptionChatController : ControllerBase
    {
        private readonly IChatV2Service _chatService;
        private readonly IChatAttachmentUploadService _attachmentUploadService;

        public ReceptionChatController(
            IChatV2Service chatService,
            IChatAttachmentUploadService attachmentUploadService)
        {
            _chatService = chatService;
            _attachmentUploadService = attachmentUploadService;
        }

        [HttpGet("conversations")]
        public async Task<ActionResult<PagedConversationsResponseDto>> GetConversations(
            [FromQuery] long? branchId = null,
            [FromQuery] string? status = null,
            [FromQuery] bool? isUnread = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                var result = await _chatService.GetReceptionConversationsAsync(
                    userId.Value,
                    ResolveRoleCode(),
                    branchId,
                    status,
                    isUnread,
                    page,
                    pageSize);

                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("conversations/{id:long}")]
        public async Task<ActionResult<ConversationDetailDto>> GetConversation(long id)
        {
            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                var detail = await _chatService.GetReceptionConversationAsync(userId.Value, ResolveRoleCode(), id);
                if (detail is null) return NotFound();
                return Ok(detail);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
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
                var messages = await _chatService.GetReceptionMessagesAsync(userId.Value, ResolveRoleCode(), id, beforeMessageId, take);
                return Ok(messages);
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

        [HttpPost("conversations/{id:long}/messages")]
        public async Task<ActionResult<ChatMessageDto>> SendMessage(long id, [FromBody] ChatSendMessageRequestDto request)
        {
            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                var message = await _chatService.SendStaffMessageAsync(userId.Value, ResolveRoleCode(), id, request);
                return Ok(message);
            }
            catch (ChatConflictException ex)
            {
                return this.ConflictProblem(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpGet("conversations/{id:long}/orders")]
        public async Task<ActionResult<IReadOnlyList<ChatOrderCardDto>>> GetConversationOrders(long id)
        {
            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                var orders = await _chatService.GetReceptionConversationOrdersAsync(
                    userId.Value,
                    ResolveRoleCode(),
                    id,
                    HttpContext.RequestAborted);
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

            var countError = _attachmentUploadService.ValidateAttachmentCount(files?.Count ?? 0);
            if (countError is not null)
            {
                return this.BadRequestProblem(countError);
            }

            try
            {
                var conversation = await _chatService.GetReceptionConversationAsync(userId.Value, ResolveRoleCode(), id, HttpContext.RequestAborted);
                if (conversation is null)
                {
                    return NotFound();
                }
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }

            var uploadResult = await _attachmentUploadService.UploadAsync(
                files.ToChatAttachmentInputs(),
                $"chat/conversations/{id}",
                HttpContext.RequestAborted);

            if (!uploadResult.Succeeded)
            {
                return this.BadRequestProblem(uploadResult.ErrorMessage!);
            }

            return Ok(uploadResult.Attachments);
        }

        [HttpPatch("conversations/{id:long}/status")]
        public async Task<ActionResult<ConversationDetailDto>> UpdateStatus(long id, [FromBody] ChatUpdateConversationStatusRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Status))
            {
                return this.BadRequestProblem("Status is required.");
            }

            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                var detail = await _chatService.UpdateConversationStatusAsync(userId.Value, ResolveRoleCode(), id, request.Status, request.Reason);
                return Ok(detail);
            }
            catch (ChatConflictException ex)
            {
                return this.ConflictProblem(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpPatch("conversations/{id:long}/read-state")]
        public async Task<ActionResult> MarkRead(long id, [FromBody] ChatUpdateReadStateRequestDto request)
        {
            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                await _chatService.MarkStaffReadAsync(userId.Value, ResolveRoleCode(), id, request.LastReadMessageId);
                return NoContent();
            }
            catch (ChatConflictException ex)
            {
                return this.ConflictProblem(ex.Message);
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

        [HttpPost("conversations/{id:long}/assignments")]
        public async Task<ActionResult<ConversationDetailDto>> AssignConversation(long id, [FromBody] ChatAssignConversationStaffRequestDto request)
        {
            if (request.AssignedStaffId <= 0)
            {
                return this.BadRequestProblem("AssignedStaffId must be greater than 0.");
            }

            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            try
            {
                var detail = await _chatService.AssignConversationAsync(userId.Value, ResolveRoleCode(), id, request.AssignedStaffId);
                return Ok(detail);
            }
            catch (ChatConflictException ex)
            {
                return this.ConflictProblem(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        private string? ResolveRoleCode()
        {
            return this.GetCurrentRoleCode() ?? this.GetCurrentRoleName() ?? this.GetCurrentRole();
        }
    }
}
