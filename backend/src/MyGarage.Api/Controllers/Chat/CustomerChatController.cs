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
        private readonly IChatCallerCustomerResolver _customerResolver;

        public CustomerChatController(
            IChatV2Service chatService,
            IChatAttachmentUploadService attachmentUploadService,
            IChatCallerCustomerResolver customerResolver)
        {
            _chatService = chatService;
            _attachmentUploadService = attachmentUploadService;
            _customerResolver = customerResolver;
        }

        [HttpGet("messenger/conversation")]
        public async Task<ActionResult<ConversationDetailDto>> GetOrCreateMessengerConversation()
        {
            if (!IsCustomerCaller())
            {
                return Forbid();
            }

            var customerId = await ResolveEffectiveCustomerIdAsync(HttpContext.RequestAborted);
            if (!customerId.HasValue) return Unauthorized();

            try
            {
                var conversation = await _chatService.GetOrCreateCustomerMessengerConversationAsync(
                    customerId.Value,
                    this.GetCurrentBranchId(),
                    HttpContext.RequestAborted);
                return Ok(conversation);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
            catch (Exception ex)
            {
                return this.BadRequestProblem("Không thể khởi tạo cuộc trò chuyện: " + ex.Message);
            }
        }

        [HttpGet("my-conversations")]
        [HttpGet("conversations")]
        public async Task<ActionResult<PagedConversationsResponseDto>> GetMyConversations(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (!IsCustomerCaller())
            {
                return Forbid();
            }

            var customerId = await ResolveEffectiveCustomerIdAsync(HttpContext.RequestAborted);
            if (!customerId.HasValue) return Unauthorized();

            var result = await _chatService.GetCustomerConversationsAsync(customerId.Value, status, page, pageSize);
            return Ok(result);
        }

        [HttpGet("conversations/{id:long}")]
        public async Task<ActionResult<ConversationDetailDto>> GetConversation(long id)
        {
            if (!IsCustomerCaller())
            {
                return Forbid();
            }

            var customerId = await ResolveEffectiveCustomerIdAsync(HttpContext.RequestAborted);
            if (!customerId.HasValue) return Unauthorized();

            var detail = await _chatService.GetCustomerConversationAsync(customerId.Value, id);
            if (detail is null) return NotFound();
            return Ok(detail);
        }

        [HttpGet("conversations/{id:long}/messages")]
        public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetMessages(
            long id,
            [FromQuery] long? beforeMessageId = null,
            [FromQuery] int take = 50)
        {
            if (!IsCustomerCaller())
            {
                return Forbid();
            }

            var customerId = await ResolveEffectiveCustomerIdAsync(HttpContext.RequestAborted);
            if (!customerId.HasValue) return Unauthorized();

            try
            {
                var messages = await _chatService.GetCustomerMessagesAsync(customerId.Value, id, beforeMessageId, take);
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

        [HttpPost("conversations")]
        public async Task<ActionResult<ConversationDetailDto>> CreateConversation([FromBody] ChatConversationCreateRequestDto request)
        {
            if (!IsCustomerCaller())
            {
                return Forbid();
            }

            var customerId = await ResolveEffectiveCustomerIdAsync(HttpContext.RequestAborted);
            if (!customerId.HasValue) return Unauthorized();

            try
            {
                var conversation = await _chatService.CreateCustomerConversationAsync(customerId.Value, request);
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
            if (!IsCustomerCaller())
            {
                return Forbid();
            }

            var customerId = await ResolveEffectiveCustomerIdAsync(HttpContext.RequestAborted);
            if (!customerId.HasValue) return Unauthorized();

            try
            {
                var message = await _chatService.SendCustomerMessageAsync(customerId.Value, id, request);
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
            if (!IsCustomerCaller())
            {
                return Forbid();
            }

            var customerId = await ResolveEffectiveCustomerIdAsync(HttpContext.RequestAborted);
            if (!customerId.HasValue) return Unauthorized();

            try
            {
                var orders = await _chatService.GetCustomerConversationOrdersAsync(
                    customerId.Value,
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
            if (!IsCustomerCaller())
            {
                return Forbid();
            }

            var customerId = await ResolveEffectiveCustomerIdAsync(HttpContext.RequestAborted);
            if (!customerId.HasValue) return Unauthorized();

            var countError = _attachmentUploadService.ValidateAttachmentCount(files?.Count ?? 0);
            if (countError is not null)
            {
                return this.BadRequestProblem(countError);
            }

            try
            {
                var conversation = await _chatService.GetCustomerConversationAsync(customerId.Value, id, HttpContext.RequestAborted);
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

        [HttpPatch("conversations/{id:long}/read-state")]
        public async Task<ActionResult> MarkRead(long id, [FromBody] ChatUpdateReadStateRequestDto request)
        {
            if (!IsCustomerCaller())
            {
                return Forbid();
            }

            var customerId = await ResolveEffectiveCustomerIdAsync(HttpContext.RequestAborted);
            if (!customerId.HasValue) return Unauthorized();

            try
            {
                await _chatService.MarkCustomerReadAsync(customerId.Value, id, request.LastReadMessageId);
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

        private async Task<long?> ResolveEffectiveCustomerIdAsync(CancellationToken cancellationToken = default)
        {
            var userId = this.GetCurrentUserId();
            if (!userId.HasValue) return null;
            var customerId = await _customerResolver.ResolveCustomerIdAsync(userId.Value, this.GetCurrentRole(), cancellationToken);
            return customerId;
        }

        private bool IsCustomerCaller() => IsCustomerRole();

        private bool IsCustomerRole()
        {
            var role = this.GetCurrentRole();
            return string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase);
        }
    }
}
