using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/chatbot")]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbotService;
    private readonly IChatbotImageUploadService _chatbotImageUploadService;
    private readonly IChatbotOrchestrator _chatbotOrchestrator;

    public ChatbotController(
        IChatbotService chatbotService,
        IChatbotImageUploadService chatbotImageUploadService,
        IChatbotOrchestrator chatbotOrchestrator)
    {
        _chatbotService = chatbotService;
        _chatbotImageUploadService = chatbotImageUploadService;
        _chatbotOrchestrator = chatbotOrchestrator;
    }

    /// <summary>
    /// Create a new chatbot session for the authenticated customer.
    /// </summary>
    /// <remarks>
    /// Customer identity is always resolved from JWT claims. Client-supplied customer identifiers are ignored.
    /// </remarks>
    /// <param name="request">Initial chatbot session state.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="201">Session created.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Authentication required.</response>
    /// <response code="403">Caller role is not Customer.</response>
    /// <response code="404">Related entity not found.</response>
    [HttpPost("sessions")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ChatbotSessionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatbotSessionResponseDto>> CreateSession(
        [FromBody] CreateChatbotSessionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(this.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var customerId = this.GetCurrentUserId();
        if (!customerId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _chatbotService.CreateSessionAsync(
                customerId.Value,
                this.GetCurrentBranchId(),
                request ?? new CreateChatbotSessionRequestDto(),
                cancellationToken);

            return CreatedAtAction(nameof(GetSession), new { id = result.Id }, result);
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

    /// <summary>
    /// Orchestrate one AI chatbot turn (intent, workflow/tool execution, and assistant reply).
    /// </summary>
    /// <remarks>
    /// Customer identity is always resolved from JWT claims. If the request does not provide
    /// a session id/token, the backend auto-creates one for the authenticated customer.
    /// </remarks>
    /// <param name="request">Orchestration request payload.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Turn orchestrated successfully.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Authentication required.</response>
    /// <response code="403">Caller role is not Customer.</response>
    /// <response code="404">Session/customer not found.</response>
    /// <response code="409">Session conflict (for example handed off/locked).</response>
    /// <response code="429">Rate limit reached.</response>
    [HttpPost("orchestrations")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(OrchestratedChatbotReplyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<OrchestratedChatbotReplyDto>> Orchestrate(
        [FromBody] OrchestrateChatbotRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(this.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var customerId = this.GetCurrentUserId();
        if (!customerId.HasValue)
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return this.BadRequestProblem("Request body is required.");
        }

        try
        {
            var safeRequest = await BuildAuthorizedOrchestrateRequestAsync(
                customerId.Value,
                request,
                cancellationToken);

            var result = await _chatbotOrchestrator.OrchestrateAsync(
                customerId.Value,
                safeRequest,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("RATE_LIMIT", StringComparison.OrdinalIgnoreCase))
            {
                return this.ProblemMessage(StatusCodes.Status429TooManyRequests, "Too many chatbot requests.");
            }

            if (ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("handed off", StringComparison.OrdinalIgnoreCase))
            {
                return this.ConflictProblem(ex.Message);
            }

            return this.BadRequestProblem(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Persist one or more chatbot message payloads into an existing customer-owned session.
    /// </summary>
    /// <remarks>
    /// For this public endpoint, the backend enforces `Role=USER` and strips any client-supplied
    /// tool call/result and session state mutation payloads. Tool execution remains server-authoritative.
    /// </remarks>
    /// <param name="request">Message payload.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Message saved.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Authentication required.</response>
    /// <response code="403">Caller role is not Customer.</response>
    /// <response code="404">Session not found.</response>
    /// <response code="409">Session state conflict (for example locked session).</response>
    /// <response code="429">Rate limit reached.</response>
    [HttpPost("messages")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(SaveChatbotMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SaveChatbotMessageResponseDto>> SaveMessages(
        [FromBody] SaveChatbotMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(this.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var customerId = this.GetCurrentUserId();
        if (!customerId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var safeRequest = BuildAuthorizedCustomerWriteRequest(request);
            var result = await _chatbotService.SaveMessagesAsync(
                customerId.Value,
                safeRequest,
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("RATE_LIMIT", StringComparison.OrdinalIgnoreCase))
            {
                return this.ProblemMessage(StatusCodes.Status429TooManyRequests, "Too many chatbot requests.");
            }

            if (ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase))
            {
                return this.ConflictProblem(ex.Message);
            }

            return this.BadRequestProblem(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Get a chatbot session and bounded message history for the authenticated customer.
    /// </summary>
    /// <param name="id">Session identifier.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Session found.</response>
    /// <response code="401">Authentication required.</response>
    /// <response code="403">Caller role is not Customer.</response>
    /// <response code="404">Session not found.</response>
    [HttpGet("sessions/{id:long}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ChatbotSessionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatbotSessionResponseDto>> GetSession(long id, CancellationToken cancellationToken)
    {
        if (!string.Equals(this.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var customerId = this.GetCurrentUserId();
        if (!customerId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _chatbotService.GetSessionByIdAsync(customerId.Value, id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Upload an image for chatbot vision-assisted triage and persist image analysis messages.
    /// </summary>
    /// <remarks>
    /// Allowed image formats are JPEG, PNG, and WEBP. The service enforces signature checks and file scanning
    /// before cloud upload and analysis.
    /// </remarks>
    /// <param name="request">Multipart form payload with session identity and image file.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Image uploaded and analyzed.</response>
    /// <response code="400">Invalid file or request payload.</response>
    /// <response code="401">Authentication required.</response>
    /// <response code="403">Caller role is not Customer.</response>
    /// <response code="404">Session or customer not found.</response>
    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    [RequestFormLimits(MultipartBodyLengthLimit = 8 * 1024 * 1024 + 1024)]
    [RequestSizeLimit(8 * 1024 * 1024 + 1024)]
    [ProducesResponseType(typeof(ChatbotImageUploadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatbotImageUploadResponseDto>> UploadImage(
        [FromForm] UploadChatbotImageRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(this.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var customerId = this.GetCurrentUserId();
        if (!customerId.HasValue)
        {
            return Unauthorized();
        }

        if (request?.File is null)
        {
            return this.BadRequestProblem("File is required.");
        }

        try
        {
            await using var stream = request.File.OpenReadStream();
            var input = new UploadChatbotImageInput(
                request.SessionId,
                request.SessionToken,
                request.Note,
                stream,
                request.File.FileName ?? string.Empty,
                request.File.ContentType ?? string.Empty,
                request.File.Length);
            var result = await _chatbotImageUploadService.UploadAndAnalyzeAsync(
                customerId.Value,
                input,
                cancellationToken);
            return Ok(result);
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

    private static SaveChatbotMessageRequestDto BuildAuthorizedCustomerWriteRequest(SaveChatbotMessageRequestDto? request)
    {
        var source = request ?? new SaveChatbotMessageRequestDto();
        return new SaveChatbotMessageRequestDto
        {
            SessionId = source.SessionId,
            SessionToken = source.SessionToken,
            Role = nameof(ChatbotMessageRole.USER),
            Content = source.Content,
            Attachments = source.Attachments,
            AllowSendToLockedSession = false,
            ToolCall = null,
            ToolResult = null,
            SessionState = null,
            SentimentScore = null,
            IntentName = null
        };
    }

    private async Task<OrchestrateChatbotRequestDto> BuildAuthorizedOrchestrateRequestAsync(
        long customerId,
        OrchestrateChatbotRequestDto request,
        CancellationToken cancellationToken)
    {
        var hasSessionIdentity = request.SessionId.GetValueOrDefault() > 0 || !string.IsNullOrWhiteSpace(request.SessionToken);
        long? sessionId = request.SessionId;
        string? sessionToken = request.SessionToken;

        if (!hasSessionIdentity)
        {
            var session = await _chatbotService.CreateSessionAsync(
                customerId,
                this.GetCurrentBranchId(),
                new CreateChatbotSessionRequestDto(),
                cancellationToken);
            sessionId = session.Id;
            sessionToken = session.SessionToken;
        }

        return new OrchestrateChatbotRequestDto
        {
            SessionId = sessionId,
            SessionToken = string.IsNullOrWhiteSpace(sessionToken) ? null : sessionToken.Trim(),
            UserMessage = request.UserMessage?.Trim() ?? string.Empty,
            Attachments = request.Attachments ?? Array.Empty<ChatbotMessageAttachmentRequestDto>(),
            AllowSendToLockedSession = false
        };
    }
}
