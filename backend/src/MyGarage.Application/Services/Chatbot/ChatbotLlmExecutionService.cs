using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotLlmExecutionService : IChatbotLlmExecutionService
{
    private static readonly HashSet<string> LlmBlockedWriteTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "update_booking_state",
        "create_appointment_draft",
        "confirm_appointment_booking",
        "create_service_order_draft",
        "create_retail_order_draft"
    };

    private static readonly HashSet<string> CommonLookupTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_customer_benefits",
        "get_customer_vouchers",
        "list_customer_vehicles",
        "get_branch_list",
        "search_standard_services",
        "get_available_slots",
        "read_booking_state",
        "get_customer_service_history",
        "get_customer_service_order_status",
        "get_customer_invoice_payment_status",
        "get_customer_warranty_history"
    };

    private static readonly HashSet<string> PurchaseLookupTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "product_pricing_inventory_lookup",
        "get_customer_invoice_payment_status"
    };

    private static readonly HashSet<string> ServiceOrderIntentTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_customer_service_history",
        "get_customer_service_order_status",
        "get_customer_invoice_payment_status",
        "get_customer_warranty_history",
        "list_customer_vehicles",
        "get_branch_list",
        "search_standard_services"
    };

    private readonly IFunctionCallingRouter _functionCallingRouter;
    private readonly IChatbotLlmPromptBuilder _chatbotLlmPromptBuilder;
    private readonly IChatbotLlmGateway _chatbotLlmGateway;
    private readonly IChatbotToolDefinitionRegistry _chatbotToolDefinitionRegistry;
    private readonly IAppLogger<ChatbotLlmExecutionService> _logger;

    public ChatbotLlmExecutionService(
        IFunctionCallingRouter functionCallingRouter,
        IChatbotLlmPromptBuilder chatbotLlmPromptBuilder,
        IChatbotLlmGateway chatbotLlmGateway,
        IChatbotToolDefinitionRegistry chatbotToolDefinitionRegistry,
        IAppLogger<ChatbotLlmExecutionService> logger)
    {
        _functionCallingRouter = functionCallingRouter;
        _chatbotLlmPromptBuilder = chatbotLlmPromptBuilder;
        _chatbotLlmGateway = chatbotLlmGateway;
        _chatbotToolDefinitionRegistry = chatbotToolDefinitionRegistry;
        _logger = logger;
    }

    public bool ShouldUseDeterministicRetrievalReply(ChatbotRetrievalResult retrieval)
    {
        if (retrieval is null)
        {
            return false;
        }

        if (retrieval.QueryType == ChatbotKnowledgeQueryType.UNKNOWN)
        {
            return false;
        }

        if (retrieval.QueryType == ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE)
        {
            return true;
        }

        return retrieval.IsGrounded || retrieval.IsAmbiguous;
    }

    public async Task<ChatbotLlmExecutionResult> ExecuteAsync(
        long customerId,
        long sessionId,
        long? branchId,
        string mergedIntent,
        string updatedIntentState,
        PromptBuildResult prompt,
        IReadOnlyList<ChatbotMessageResponseDto> sessionMessages,
        IReadOnlyList<ChatbotMessageAttachmentRequestDto> attachments,
        CancellationToken cancellationToken = default)
    {
        var route = await _functionCallingRouter.RouteAsync(
            customerId,
            branchId,
            mergedIntent,
            updatedIntentState,
            prompt,
            cancellationToken);

        var hasDeterministicFunctionRoute = route.FunctionCalls.Count > 0
            || !string.IsNullOrWhiteSpace(route.AssistantReplyOverride);

        ChatbotLlmGatewayResponse? primaryLlmResponse = null;
        var preferVisionModel = ShouldPreferVisionModel(attachments);

        if (!hasDeterministicFunctionRoute)
        {
            var llmMessages = _chatbotLlmPromptBuilder.BuildMessages(
                prompt.SystemPrompt,
                prompt.UserPrompt,
                sessionMessages);
            var toolDefinitions = GetLlmToolDefinitionsForIntent(mergedIntent);
            primaryLlmResponse = await _chatbotLlmGateway.GenerateAsync(
                new ChatbotLlmGatewayRequest
                {
                    CustomerId = customerId,
                    SessionId = sessionId,
                    BranchId = branchId,
                    Intent = mergedIntent,
                    PreferVisionModel = preferVisionModel,
                    Messages = llmMessages,
                    ToolDefinitions = toolDefinitions,
                    AllowToolCalls = true
                },
                cancellationToken);

            route = BuildRouteFromPrimaryLlmResponse(
                customerId,
                sessionId,
                toolDefinitions,
                primaryLlmResponse);
        }

        var executedToolResults = await ExecuteToolCallsAsync(
            customerId,
            sessionId,
            branchId,
            route.FunctionCalls,
            cancellationToken);

        if (primaryLlmResponse?.Succeeded == true && executedToolResults.Count > 0)
        {
            var followUpMessages = _chatbotLlmPromptBuilder.BuildMessages(
                prompt.SystemPrompt,
                prompt.UserPrompt,
                sessionMessages,
                executedToolResults.Select(x => new ChatbotToolExecutionRecord
                {
                    ToolName = x.ToolName,
                    ToolArgs = x.ToolArgs,
                    ToolResult = x.ToolResult
                }).ToList());

            var followUp = await _chatbotLlmGateway.GenerateAsync(
                new ChatbotLlmGatewayRequest
                {
                    CustomerId = customerId,
                    SessionId = sessionId,
                    BranchId = branchId,
                    Intent = mergedIntent,
                    PreferVisionModel = preferVisionModel,
                    Messages = followUpMessages,
                    ToolDefinitions = [],
                    AllowToolCalls = false
                },
                cancellationToken);

            if (followUp.Succeeded && !string.IsNullOrWhiteSpace(followUp.AssistantReply))
            {
                route.AssistantReplyOverride = followUp.AssistantReply;
            }
        }

        return new ChatbotLlmExecutionResult
        {
            Route = route,
            ExecutedToolResults = executedToolResults
        };
    }

    public string BuildAssistantReply(
        GuardrailValidationResult guardrail,
        IntentDetectionResult detection,
        string intent,
        string? retrievalReply,
        string? overrideReply)
    {
        if (!string.IsNullOrWhiteSpace(retrievalReply))
        {
            return retrievalReply.Trim();
        }

        if (!string.IsNullOrWhiteSpace(overrideReply))
        {
            return overrideReply.Trim();
        }

        if (guardrail.IsRejected)
        {
            return guardrail.RejectionMessage ?? "Yêu cầu không hợp lệ.";
        }

        if (detection.IsLowConfidence || intent == nameof(ChatbotIntentType.UNKNOWN))
        {
            return "Mình hỗ trợ trong phạm vi garage: tư vấn lỗi xe, báo giá, đặt lịch, đơn dịch vụ, phụ tùng và bảo hành. Bạn mô tả tình trạng xe hoặc nhu cầu cụ thể nhé.";
        }

        return intent switch
        {
            nameof(ChatbotIntentType.CONSULTATION) => "Mình đã hiểu bạn cần tư vấn. Mình sẽ hỏi nhanh vài thông tin xe để đưa khuyến nghị phù hợp.",
            nameof(ChatbotIntentType.INQUIRY) => "Mình đã ghi nhận câu hỏi của bạn. Mình sẽ cung cấp thông tin chi tiết theo yêu cầu.",
            nameof(ChatbotIntentType.PURCHASE) => "Mình đã ghi nhận nhu cầu mua hàng/phụ tùng. Mình sẽ xác nhận sản phẩm và báo giá phù hợp.",
            nameof(ChatbotIntentType.SERVICE_ORDER) => "Mình đã nhận yêu cầu liên quan đơn dịch vụ. Mình sẽ tra cứu trạng thái và thông tin liên quan.",
            nameof(ChatbotIntentType.BOOKING) => "Mình đã ghi nhận yêu cầu đặt lịch. Mình sẽ tiếp tục thu thập thông tin để đề xuất khung giờ.",
            nameof(ChatbotIntentType.HANDOFF) => "Mình đã nhận thông tin. Bạn vui lòng cung cấp thêm chi tiết để mình hỗ trợ chính xác hơn.",
            _ => "Mình đã nhận thông tin. Bạn vui lòng cung cấp thêm chi tiết để mình hỗ trợ chính xác hơn."
        };
    }

    private FunctionRoutingResult BuildRouteFromPrimaryLlmResponse(
        long customerId,
        long sessionId,
        IReadOnlyList<ChatbotToolDefinition> toolDefinitions,
        ChatbotLlmGatewayResponse primaryLlmResponse)
    {
        if (!primaryLlmResponse.Succeeded)
        {
            return new FunctionRoutingResult
            {
                AssistantReplyOverride = BuildLlmGatewayFailureReply(primaryLlmResponse)
            };
        }

        var allowedTools = toolDefinitions
            .Select(x => x.ToolName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validatedToolCalls = primaryLlmResponse.ToolCalls
            .Where(x => allowedTools.Contains(x.ToolName))
            .ToList();
        if (validatedToolCalls.Count != primaryLlmResponse.ToolCalls.Count)
        {
            _logger.LogWarning(
                "LLM suggested unsupported tool call(s). CustomerId={CustomerId}, SessionId={SessionId}, SuggestedCount={SuggestedCount}, AcceptedCount={AcceptedCount}",
                customerId,
                sessionId,
                primaryLlmResponse.ToolCalls.Count,
                validatedToolCalls.Count);
        }

        return new FunctionRoutingResult
        {
            AssistantReplyOverride = primaryLlmResponse.AssistantReply,
            FunctionCalls = validatedToolCalls
                .Select(x => new FunctionCallDescriptor
                {
                    ToolName = x.ToolName,
                    ToolArgs = x.ToolArgs
                })
                .ToList()
        };
    }

    private async Task<List<ChatbotToolExecutionResult>> ExecuteToolCallsAsync(
        long customerId,
        long sessionId,
        long? branchId,
        IReadOnlyList<FunctionCallDescriptor> functionCalls,
        CancellationToken cancellationToken)
    {
        var executedToolResults = new List<ChatbotToolExecutionResult>();
        foreach (var call in functionCalls)
        {
            var exec = await _chatbotToolDefinitionRegistry.ExecuteAsync(
                new ChatbotToolExecutionRequest
                {
                    CustomerId = customerId,
                    SessionId = sessionId,
                    BranchId = branchId,
                    ToolCall = new ChatbotLlmToolCall
                    {
                        ToolName = call.ToolName,
                        ToolArgs = call.ToolArgs
                    }
                },
                cancellationToken);
            executedToolResults.Add(exec);
        }

        return executedToolResults;
    }

    private IReadOnlyList<ChatbotToolDefinition> GetLlmToolDefinitionsForIntent(string intent)
    {
        var definitions = _chatbotToolDefinitionRegistry
            .GetToolDefinitions()
            .Where(x => !LlmBlockedWriteTools.Contains(x.ToolName));

        var normalizedIntent = string.IsNullOrWhiteSpace(intent)
            ? nameof(ChatbotIntentType.UNKNOWN)
            : intent.Trim().ToUpperInvariant();

        if (normalizedIntent == nameof(ChatbotIntentType.PURCHASE))
        {
            definitions = definitions.Where(x =>
                PurchaseLookupTools.Contains(x.ToolName) || CommonLookupTools.Contains(x.ToolName));
        }
        else if (normalizedIntent == nameof(ChatbotIntentType.SERVICE_ORDER))
        {
            definitions = definitions.Where(x =>
                ServiceOrderIntentTools.Contains(x.ToolName) || CommonLookupTools.Contains(x.ToolName));
        }
        else if (normalizedIntent is nameof(ChatbotIntentType.INQUIRY)
                 or nameof(ChatbotIntentType.CONSULTATION)
                 or nameof(ChatbotIntentType.UNKNOWN))
        {
            definitions = definitions.Where(x => CommonLookupTools.Contains(x.ToolName));
        }
        else if (normalizedIntent == nameof(ChatbotIntentType.BOOKING))
        {
            definitions = definitions.Where(x =>
                string.Equals(x.ToolName, "list_customer_vehicles", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.ToolName, "get_branch_list", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.ToolName, "search_standard_services", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.ToolName, "get_available_slots", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.ToolName, "read_booking_state", StringComparison.OrdinalIgnoreCase));
        }
        else if (normalizedIntent == nameof(ChatbotIntentType.HANDOFF))
        {
            return [];
        }

        return definitions.ToList();
    }

    private static string BuildLlmGatewayFailureReply(ChatbotLlmGatewayResponse? response)
    {
        if (response is null)
        {
            return "Hiện tại AI Gemini chưa phản hồi. Vui lòng thử lại sau ít phút.";
        }

        if (string.Equals(response.FailureCode, "PROVIDER_API_KEY_MISSING", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.FailureCode, "MODEL_CONFIG_NOT_FOUND", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.FailureCode, "PROVIDER_ENDPOINT_NOT_CONFIGURED", StringComparison.OrdinalIgnoreCase))
        {
            return "Tính năng AI đang tạm thời gián đoạn. Vui lòng thử lại sau ít phút hoặc chọn gặp tư vấn viên.";
        }

        return $"AI Gemini tạm thời chưa phản hồi ({response.FailureCode}). Vui lòng thử lại sau.";
    }

    private static bool ShouldPreferVisionModel(IReadOnlyList<ChatbotMessageAttachmentRequestDto> attachments)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return false;
        }

        return attachments.Any(x =>
            !string.IsNullOrWhiteSpace(x.ContentType) &&
            x.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));
    }
}
