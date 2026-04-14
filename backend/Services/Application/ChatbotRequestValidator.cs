using System.Collections.Generic;
using System.Linq;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotRequestValidator : IChatbotRequestValidator
{
    private const int MaxMessageContentLength = 8000;
    private const int MaxAttachmentsPerMessage = 5;
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedAttachmentContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf"
    ];

    private readonly IChatbotToolAllowlist _toolAllowlist;

    public ChatbotRequestValidator(IChatbotToolAllowlist toolAllowlist)
    {
        _toolAllowlist = toolAllowlist;
    }

    public IReadOnlyList<string> ValidateCreateSession(CreateChatbotSessionRequestDto request)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Request body is required.");
            return errors;
        }

        if (!string.IsNullOrWhiteSpace(request.IntentState) && request.IntentState.Length > 20000)
        {
            errors.Add("IntentState is too large.");
        }

        return errors;
    }

    public IReadOnlyList<string> ValidateSaveMessage(SaveChatbotMessageRequestDto request)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Request body is required.");
            return errors;
        }

        if (request.SessionId.GetValueOrDefault() <= 0 && string.IsNullOrWhiteSpace(request.SessionToken))
        {
            errors.Add("Either SessionId or SessionToken is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.SessionToken) && request.SessionToken!.Length > 128)
        {
            errors.Add("SessionToken is too long.");
        }

        var normalizedRole = request.Role?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedRole))
        {
            errors.Add("Role is required.");
        }
        else if (!IsAllowedRole(normalizedRole))
        {
            errors.Add("Unsupported role.");
        }

        if (!string.IsNullOrWhiteSpace(request.Content) && request.Content!.Length > MaxMessageContentLength)
        {
            errors.Add($"Content exceeds max length {MaxMessageContentLength}.");
        }

        ValidateAttachments(request.Attachments, errors);

        if (request.SentimentScore is < -1m or > 1m)
        {
            errors.Add("SentimentScore must be between -1 and 1.");
        }

        var hasContent = !string.IsNullOrWhiteSpace(request.Content);
        var hasToolCall = request.ToolCall is not null;
        var hasToolResult = request.ToolResult is not null;
        if (!hasContent && !hasToolCall && !hasToolResult)
        {
            errors.Add("At least one message payload (content/tool call/tool result) is required.");
        }

        if (hasToolCall && string.IsNullOrWhiteSpace(request.ToolCall!.ToolName))
        {
            errors.Add("Tool call name is required.");
        }
        else if (hasToolCall && !_toolAllowlist.IsAllowed(request.ToolCall!.ToolName))
        {
            errors.Add("Tool call is not allowed.");
        }

        if (hasToolResult && string.IsNullOrWhiteSpace(request.ToolResult!.ToolName))
        {
            errors.Add("Tool result name is required.");
        }
        else if (hasToolResult && !_toolAllowlist.IsAllowed(request.ToolResult!.ToolName))
        {
            errors.Add("Tool result is not allowed.");
        }

        if (request.SessionState?.Status is not null)
        {
            var status = request.SessionState.Status.Trim().ToUpperInvariant();
            if (!IsAllowedStatus(status))
            {
                errors.Add("Unsupported session status.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SessionState?.CurrentIntent) && request.SessionState!.CurrentIntent!.Length > 100)
        {
            errors.Add("CurrentIntent is too long.");
        }

        if (!string.IsNullOrWhiteSpace(request.SessionState?.IntentState) && request.SessionState!.IntentState!.Length > 20000)
        {
            errors.Add("IntentState is too large.");
        }

        return errors;
    }

    private static void ValidateAttachments(
        IReadOnlyList<ChatbotMessageAttachmentRequestDto>? attachments,
        List<string> errors)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return;
        }

        if (attachments.Count > MaxAttachmentsPerMessage)
        {
            errors.Add($"Attachments exceed max count {MaxAttachmentsPerMessage}.");
        }

        for (var i = 0; i < attachments.Count; i++)
        {
            var attachment = attachments[i];
            if (attachment is null)
            {
                errors.Add($"Attachment at index {i} is invalid.");
                continue;
            }

            if (attachment.FileSize is < 0)
            {
                errors.Add($"Attachment at index {i} has invalid file size.");
            }
            else if (attachment.FileSize > MaxAttachmentBytes)
            {
                errors.Add($"Attachment at index {i} exceeds max size {MaxAttachmentBytes} bytes.");
            }

            if (!string.IsNullOrWhiteSpace(attachment.ContentType))
            {
                var normalized = attachment.ContentType.Trim().ToLowerInvariant();
                if (!AllowedAttachmentContentTypes.Contains(normalized))
                {
                    errors.Add($"Attachment at index {i} has unsupported content type.");
                }
            }
        }
    }

    private static bool IsAllowedRole(string role)
    {
        return role is nameof(ChatbotMessageRole.USER)
            or nameof(ChatbotMessageRole.ASSISTANT)
            or nameof(ChatbotMessageRole.SYSTEM)
            or nameof(ChatbotMessageRole.TOOL_CALL)
            or nameof(ChatbotMessageRole.TOOL_RESULT);
    }

    private static bool IsAllowedStatus(string status)
    {
        return status is nameof(ChatbotSessionStatus.ACTIVE)
            or nameof(ChatbotSessionStatus.IDLE)
            or nameof(ChatbotSessionStatus.HANDED_OFF)
            or nameof(ChatbotSessionStatus.EXPIRED)
            or nameof(ChatbotSessionStatus.CLOSED)
            or nameof(ChatbotSessionStatus.ARCHIVED);
    }
}
