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
    [Route("api/v1/chat")]
    public class CustomerChatController : ControllerBase
    {
        private readonly IChatV2Service _chatService;
        private readonly IChatAttachmentUploadService _attachmentUploadService;

        public CustomerChatController(
            IChatV2Service chatService,
            IChatAttachmentUploadService attachmentUploadService)
        {
            _chatService = chatService;
            _attachmentUploadService = attachmentUploadService;
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
                return this.BadRequestProblem(ex.Message);
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
                return this.ConflictProblem(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
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
                return this.ConflictProblem(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
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

            var countError = _attachmentUploadService.ValidateAttachmentCount(files?.Count ?? 0);
            if (countError is not null)
            {
                return this.BadRequestProblem(countError);
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

        [HttpPatch("conversations/{id:long}/read-state")]
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
