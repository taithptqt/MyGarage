using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Requests.Chat;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class ChatbotHandoffService : IChatbotHandoffService
{
    private readonly IChatV2Service _chatV2Service;
    private readonly IAppLogger<ChatbotHandoffService> _logger;

    public ChatbotHandoffService(
        IChatV2Service chatV2Service,
        IAppLogger<ChatbotHandoffService> logger)
    {
        _chatV2Service = chatV2Service;
        _logger = logger;
    }

    public async Task<ChatbotHandoffResult> InitiateAsync(
        long customerId,
        long? preferredBranchId,
        string userMessage,
        string intent,
        string intentStateJson,
        decimal confidence,
        CancellationToken cancellationToken = default)
    {
        var normalizedInput = NormalizeForRouting(userMessage);
        var triggerType = ResolveTriggerType(normalizedInput, intent);
        var conversation = await _chatV2Service.GetOrCreateCustomerMessengerConversationAsync(
            customerId,
            preferredBranchId,
            cancellationToken);

        var summaryRoot = BuildSummaryPayload(
            userMessage,
            intent,
            intentStateJson,
            confidence,
            triggerType);
        var summaryJson = summaryRoot.ToJsonString();

        var handoffNote = BuildHandoffNote(userMessage, intent, confidence, triggerType);
        await _chatV2Service.SendCustomerMessageAsync(
            customerId,
            conversation.Id,
            new ChatSendMessageRequestDto
            {
                MessageType = nameof(ChatMessageType.TEXT),
                Content = handoffNote
            },
            cancellationToken);

        var assistantReply = BuildAssistantReply(conversation.Id);
        _logger.LogInformation(
            "Chatbot handoff initiated. CustomerId={CustomerId}, ConversationId={ConversationId}, Trigger={Trigger}",
            customerId,
            conversation.Id,
            triggerType);

        return new ChatbotHandoffResult
        {
            ConversationId = conversation.Id,
            SummaryJson = summaryJson,
            AssistantReply = assistantReply,
            TriggerType = triggerType
        };
    }

    private static JsonObject BuildSummaryPayload(
        string userMessage,
        string intent,
        string intentStateJson,
        decimal confidence,
        ChatbotHandoffTriggerType triggerType)
    {
        var preview = string.IsNullOrWhiteSpace(userMessage)
            ? string.Empty
            : userMessage.Trim();
        if (preview.Length > 500)
        {
            preview = preview[..500];
        }

        return new JsonObject
        {
            ["triggerType"] = triggerType.ToString(),
            ["intent"] = intent,
            ["confidence"] = confidence,
            ["userMessagePreview"] = preview,
            ["intentState"] = TryParseJson(intentStateJson),
            ["createdAtUtc"] = DateTime.UtcNow
        };
    }

    private static JsonNode? TryParseJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(value);
        }
        catch
        {
            return value;
        }
    }

    private static string BuildHandoffNote(
        string userMessage,
        string intent,
        decimal confidence,
        ChatbotHandoffTriggerType triggerType)
    {
        var safeMessage = string.IsNullOrWhiteSpace(userMessage)
            ? "(không có nội dung)"
            : userMessage.Trim();
        if (safeMessage.Length > 1000)
        {
            safeMessage = safeMessage[..1000];
        }

        return
            "[CHATBOT HANDOFF]\n" +
            $"Lý do chuyển: {triggerType}\n" +
            $"Intent cuối: {intent} (confidence={confidence:0.00})\n" +
            $"Nội dung khách: {safeMessage}\n" +
            $"Thời điểm UTC: {DateTime.UtcNow:O}";
    }

    private static string BuildAssistantReply(long conversationId)
    {
        return
            "Mình đã chuyển cuộc trò chuyện sang tư vấn viên thật. " +
            $"Mã phiên hỗ trợ: #{conversationId}. " +
            "Bạn vui lòng chờ trong giây lát, nhân viên sẽ phản hồi sớm nhất có thể.";
    }

    private static ChatbotHandoffTriggerType ResolveTriggerType(string normalizedInput, string intent)
    {
        if (string.Equals(intent, nameof(ChatbotIntentType.HANDOFF), StringComparison.OrdinalIgnoreCase))
        {
            return ChatbotHandoffTriggerType.CUSTOMER_REQUESTED_HUMAN;
        }

        if (normalizedInput.Contains("nhân viên", StringComparison.Ordinal)
            || normalizedInput.Contains("nhan vien", StringComparison.Ordinal)
            || normalizedInput.Contains("người thật", StringComparison.Ordinal)
            || normalizedInput.Contains("nguoi that", StringComparison.Ordinal)
            || normalizedInput.Contains("gọi tổng đài", StringComparison.Ordinal)
            || normalizedInput.Contains("goi tong dai", StringComparison.Ordinal))
        {
            return ChatbotHandoffTriggerType.CUSTOMER_REQUESTED_HUMAN;
        }

        if (normalizedInput.Contains("khiếu nại", StringComparison.Ordinal)
            || normalizedInput.Contains("khieu nai", StringComparison.Ordinal)
            || normalizedInput.Contains("bực", StringComparison.Ordinal)
            || normalizedInput.Contains("không hài lòng", StringComparison.Ordinal)
            || normalizedInput.Contains("khong hai long", StringComparison.Ordinal))
        {
            return ChatbotHandoffTriggerType.NEGATIVE_SENTIMENT;
        }

        return ChatbotHandoffTriggerType.COMPLAINT_OUT_OF_SCOPE;
    }

    private static string NormalizeForRouting(string? input)
    {
        return (input ?? string.Empty).Trim().ToLowerInvariant();
    }
}
