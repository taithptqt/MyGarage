using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotOrchestrator : IChatbotOrchestrator
{
    private static readonly Regex MarkdownBoldRegex = new(@"\*\*(?<value>[^*]{3,160})\*\*", RegexOptions.Compiled);
    private static readonly Regex MarkdownCodeRegex = new(@"`(?<value>[^`]{3,160})`", RegexOptions.Compiled);
    private static readonly Regex ServiceOfferRegex = new(
        @"(?:dich vu)\s+(?<value>.+?)\s+(?:cua)\s+(?:chung toi)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
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

    private readonly IChatbotService _chatbotService;
    private readonly IIntentDetector _intentDetector;
    private readonly IGuardrailValidator _guardrailValidator;
    private readonly IChatbotBookingWorkflowService _bookingWorkflowService;
    private readonly IChatbotHandoffService _chatbotHandoffService;
    private readonly IChatbotRetrievalService _retrievalService;
    private readonly IChatbotAnswerComposer _answerComposer;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IFunctionCallingRouter _functionCallingRouter;
    private readonly IChatbotLlmPromptBuilder _chatbotLlmPromptBuilder;
    private readonly IChatbotLlmGateway _chatbotLlmGateway;
    private readonly IChatbotToolDefinitionRegistry _chatbotToolDefinitionRegistry;
    private readonly ILogger<ChatbotOrchestrator> _logger;

    public ChatbotOrchestrator(
        IChatbotService chatbotService,
        IIntentDetector intentDetector,
        IGuardrailValidator guardrailValidator,
        IChatbotBookingWorkflowService bookingWorkflowService,
        IChatbotHandoffService chatbotHandoffService,
        IChatbotRetrievalService retrievalService,
        IChatbotAnswerComposer answerComposer,
        IPromptBuilder promptBuilder,
        IFunctionCallingRouter functionCallingRouter,
        IChatbotLlmPromptBuilder chatbotLlmPromptBuilder,
        IChatbotLlmGateway chatbotLlmGateway,
        IChatbotToolDefinitionRegistry chatbotToolDefinitionRegistry,
        ILogger<ChatbotOrchestrator> logger)
    {
        _chatbotService = chatbotService;
        _intentDetector = intentDetector;
        _guardrailValidator = guardrailValidator;
        _bookingWorkflowService = bookingWorkflowService;
        _chatbotHandoffService = chatbotHandoffService;
        _retrievalService = retrievalService;
        _answerComposer = answerComposer;
        _promptBuilder = promptBuilder;
        _functionCallingRouter = functionCallingRouter;
        _chatbotLlmPromptBuilder = chatbotLlmPromptBuilder;
        _chatbotLlmGateway = chatbotLlmGateway;
        _chatbotToolDefinitionRegistry = chatbotToolDefinitionRegistry;
        _logger = logger;
    }

    public async Task<OrchestratedChatbotReplyDto> OrchestrateAsync(
        long customerId,
        OrchestrateChatbotRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.UserMessage))
        {
            throw new InvalidOperationException("UserMessage is required.");
        }

        var session = await ResolveSessionAsync(customerId, request, cancellationToken)
            ?? throw new KeyNotFoundException("Session not found.");
        if (session.CustomerId != customerId)
        {
            throw new InvalidOperationException("Session ownership mismatch.");
        }

        var guardrail = _guardrailValidator.Validate(request.UserMessage);
        var contextualUserInput = BuildContextualUserInput(guardrail.SanitizedInput, session.Messages);
        var detection = _intentDetector.Detect(contextualUserInput, session.IntentState);
        var normalizedUserInput = NormalizeForRouting(contextualUserInput);
        var scopedGreetingReply = BuildGarageGreetingReply(normalizedUserInput);
        if (IsDirectBookingRequest(normalizedUserInput))
        {
            detection.Intent = nameof(ChatbotIntentType.BOOKING);
            detection.IsLowConfidence = false;
            detection.Confidence = Math.Max(detection.Confidence, 0.85m);
            detection.Strategy = "DIRECT_BOOKING_REQUEST";
            detection.Explanation = "User explicitly requested automatic booking.";
        }

        var bookingOfferHandoffPrefill = TryBuildBookingOfferHandoffPrefill(
            contextualUserInput,
            session.Messages);
        if (!string.IsNullOrWhiteSpace(bookingOfferHandoffPrefill))
        {
            detection.Intent = nameof(ChatbotIntentType.BOOKING);
            detection.IsLowConfidence = false;
            detection.Confidence = Math.Max(detection.Confidence, 0.88m);
            detection.Strategy = "CONSULTATION_BOOKING_HANDOFF";
            detection.Explanation = "User accepted a prior booking offer and the session was promoted into booking.";
        }

        var bookingInProgress = IsBookingInProgress(session.IntentState, session.CurrentIntent);
        var bookingExitRequested = IsBookingExitCommand(contextualUserInput);
        var bookingContextSwitchRequested = IsBookingContextSwitchCommand(normalizedUserInput);
        var bookingContextSwitchIntent = bookingContextSwitchRequested
            ? ResolveContextSwitchIntent(normalizedUserInput)
            : nameof(ChatbotIntentType.UNKNOWN);
        var continueBooking = ShouldContinueBookingFlow(
            session.IntentState,
            session.CurrentIntent,
            contextualUserInput);
        if (bookingInProgress && bookingContextSwitchRequested)
        {
            detection.Intent = bookingContextSwitchIntent;
            detection.IsLowConfidence = false;
            detection.Confidence = Math.Max(detection.Confidence, 0.80m);
            detection.Strategy = "BOOKING_CONTEXT_SWITCH";
            detection.Explanation = "User switched away from booking context.";
        }
        else if (bookingInProgress && !bookingExitRequested)
        {
            detection.Intent = nameof(ChatbotIntentType.BOOKING);
            detection.IsLowConfidence = false;
            detection.Confidence = Math.Max(detection.Confidence, 0.90m);
            detection.Strategy = "BOOKING_INTENT_LOCK";
            detection.Explanation = "Booking flow is active and locked until user exits explicitly.";
        }
        else if (continueBooking)
        {
            detection.Intent = nameof(ChatbotIntentType.BOOKING);
            detection.IsLowConfidence = false;
            detection.Confidence = Math.Max(detection.Confidence, 0.75m);
            detection.Strategy = "STATEFUL_BOOKING_CONTINUATION";
            detection.Explanation = "Booking flow is in progress in session state.";
        }
        else if (bookingContextSwitchRequested
            && string.Equals(detection.Intent, nameof(ChatbotIntentType.BOOKING), StringComparison.OrdinalIgnoreCase))
        {
            detection.Intent = bookingContextSwitchIntent;
            detection.IsLowConfidence = false;
            detection.Confidence = Math.Max(detection.Confidence, 0.80m);
            detection.Strategy = "BOOKING_CONTEXT_SWITCH";
            detection.Explanation = "User switched away from booking context.";
        }

        var mergedIntent = NormalizeIntent(detection.Intent);
        var shouldHandoff = guardrail.ShouldHandoff
            || string.Equals(mergedIntent, nameof(ChatbotIntentType.HANDOFF), StringComparison.OrdinalIgnoreCase)
            || IsExplicitHandoffRequest(normalizedUserInput);
        if (shouldHandoff)
        {
            mergedIntent = nameof(ChatbotIntentType.HANDOFF);
            detection.Intent = nameof(ChatbotIntentType.HANDOFF);
            detection.IsLowConfidence = false;
            detection.Confidence = Math.Max(detection.Confidence, 0.90m);
            detection.Strategy = "EXPLICIT_HUMAN_HANDOFF";
            detection.Explanation = "User requested a human agent handoff.";
        }

        var fallbackTriggered = detection.IsLowConfidence
            && mergedIntent == nameof(ChatbotIntentType.UNKNOWN)
            && !shouldHandoff;
        var updatedIntentState = BuildUpdatedIntentState(
            session.IntentState,
            mergedIntent,
            fallbackTriggered,
            shouldHandoff,
            guardrail.ViolationCode,
            contextualUserInput);
        if (!string.IsNullOrWhiteSpace(bookingOfferHandoffPrefill)
            && string.Equals(mergedIntent, nameof(ChatbotIntentType.BOOKING), StringComparison.OrdinalIgnoreCase))
        {
            updatedIntentState = SeedBookingStateFromOfferHandoff(updatedIntentState, bookingOfferHandoffPrefill);
        }
        updatedIntentState = RemoveBookingStateWhenLeavingFlow(updatedIntentState, mergedIntent);

        var prompt = _promptBuilder.Build(contextualUserInput, detection, updatedIntentState, guardrail);

        var retrieval = await _retrievalService.RetrieveAsync(
            customerId,
            session.BranchId,
            contextualUserInput,
            cancellationToken);
        var deterministicRetrievalReply = ShouldUseDeterministicRetrievalReply(retrieval)
            ? await _answerComposer.ComposeAsync(retrieval, session.BranchId, cancellationToken)
            : null;

        FunctionRoutingResult route;
        ChatbotLlmGatewayResponse? primaryLlmResponse = null;
        string? retrievalComposedReply = null;
        ChatbotBookingWorkflowResponseDto? bookingWorkflow = null;
        var executedToolResults = new List<ChatbotToolExecutionResult>();
        if (shouldHandoff)
        {
            route = new FunctionRoutingResult();
            retrievalComposedReply = null;
        }
        else if (!guardrail.IsRejected && mergedIntent == nameof(ChatbotIntentType.BOOKING))
        {
            route = new FunctionRoutingResult();
            bookingWorkflow = await _bookingWorkflowService.RunAsync(
                customerId,
                session.Id,
                session.BranchId,
                updatedIntentState,
                contextualUserInput,
                cancellationToken);
            updatedIntentState = bookingWorkflow.UpdatedIntentState;
            retrievalComposedReply = bookingWorkflow.Prompt;
        }
        else if (guardrail.IsRejected)
        {
            route = new FunctionRoutingResult();
            retrievalComposedReply = null;
        }
        else if (!string.IsNullOrWhiteSpace(scopedGreetingReply))
        {
            route = new FunctionRoutingResult
            {
                AssistantReplyOverride = scopedGreetingReply
            };
        }
        else if (!string.IsNullOrWhiteSpace(deterministicRetrievalReply))
        {
            route = new FunctionRoutingResult
            {
                AssistantReplyOverride = deterministicRetrievalReply
            };
        }
        else
        {
            route = await _functionCallingRouter.RouteAsync(
                customerId,
                session.BranchId,
                mergedIntent,
                updatedIntentState,
                prompt,
                cancellationToken);

            var hasDeterministicFunctionRoute = route.FunctionCalls.Count > 0
                || !string.IsNullOrWhiteSpace(route.AssistantReplyOverride);

            if (!hasDeterministicFunctionRoute)
            {
                var llmMessages = _chatbotLlmPromptBuilder.BuildMessages(
                    prompt.SystemPrompt,
                    prompt.UserPrompt,
                    session.Messages);
                var toolDefinitions = GetLlmToolDefinitionsForIntent(mergedIntent);
                primaryLlmResponse = await _chatbotLlmGateway.GenerateAsync(
                    new ChatbotLlmGatewayRequest
                    {
                        CustomerId = customerId,
                        SessionId = session.Id,
                        BranchId = session.BranchId,
                        Intent = mergedIntent,
                        PreferVisionModel = ShouldPreferVisionModel(request.Attachments),
                        Messages = llmMessages,
                        ToolDefinitions = toolDefinitions,
                        AllowToolCalls = true
                    },
                    cancellationToken);

                if (primaryLlmResponse.Succeeded)
                {
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
                            session.Id,
                            primaryLlmResponse.ToolCalls.Count,
                            validatedToolCalls.Count);
                    }

                    route = new FunctionRoutingResult
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
                else
                {
                    route = new FunctionRoutingResult
                    {
                        AssistantReplyOverride = BuildLlmGatewayFailureReply(primaryLlmResponse)
                    };
                }
            }
        }

        foreach (var call in route.FunctionCalls)
        {
            var exec = await _chatbotToolDefinitionRegistry.ExecuteAsync(
                new ChatbotToolExecutionRequest
                {
                    CustomerId = customerId,
                    SessionId = session.Id,
                    BranchId = session.BranchId,
                    ToolCall = new ChatbotLlmToolCall
                    {
                        ToolName = call.ToolName,
                        ToolArgs = call.ToolArgs
                    }
                },
                cancellationToken);
            executedToolResults.Add(exec);
        }

        if (primaryLlmResponse?.Succeeded == true && executedToolResults.Count > 0)
        {
            var followUpMessages = _chatbotLlmPromptBuilder.BuildMessages(
                prompt.SystemPrompt,
                prompt.UserPrompt,
                session.Messages,
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
                    SessionId = session.Id,
                    BranchId = session.BranchId,
                    Intent = mergedIntent,
                    PreferVisionModel = ShouldPreferVisionModel(request.Attachments),
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

        ChatbotHandoffResult? handoffResult = null;
        if (shouldHandoff)
        {
            try
            {
                handoffResult = await _chatbotHandoffService.InitiateAsync(
                    customerId,
                    session.BranchId,
                    contextualUserInput,
                    mergedIntent,
                    updatedIntentState,
                    detection.Confidence,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Chatbot handoff initiation failed. CustomerId={CustomerId}, SessionId={SessionId}",
                    customerId,
                    session.Id);
            }
        }

        var assistantReply = shouldHandoff
            ? handoffResult?.AssistantReply
              ?? "Mình dã ghi nh?n yêu c?u g?p tu v?n viên. Hi?n k?t n?i chuy?n ti?p dang gián do?n, b?n vui lòng th? l?i sau ít phút."
            : BuildAssistantReply(
                guardrail,
                detection,
                mergedIntent,
                retrievalComposedReply,
                route.AssistantReplyOverride);
        var functionCalls = route.FunctionCalls.Select(x => new OrchestratedFunctionCallDto
        {
            ToolName = x.ToolName,
            ToolArgs = x.ToolArgs
        }).ToList();

        var saveRequest = new SaveChatbotMessageRequestDto
        {
            SessionId = session.Id,
            Role = nameof(ChatbotMessageRole.USER),
            Content = guardrail.SanitizedInput,
            IntentName = mergedIntent,
            Attachments = request.Attachments,
            SessionState = new ChatbotSessionStateUpdateRequestDto
            {
                CurrentIntent = mergedIntent,
                IntentState = updatedIntentState
            },
            AllowSendToLockedSession = request.AllowSendToLockedSession
        };

        await _chatbotService.SaveMessagesAsync(customerId, saveRequest, cancellationToken);

        for (var i = 0; i < route.FunctionCalls.Count; i++)
        {
            var call = route.FunctionCalls[i];
            await _chatbotService.SaveMessagesAsync(customerId, new SaveChatbotMessageRequestDto
            {
                SessionId = session.Id,
                Role = nameof(ChatbotMessageRole.ASSISTANT),
                ToolCall = new ChatbotToolCallRequestDto
                {
                    ToolName = call.ToolName,
                    ToolArgs = call.ToolArgs
                },
                AllowSendToLockedSession = request.AllowSendToLockedSession
            }, cancellationToken);

            if (i < executedToolResults.Count)
            {
                var toolResult = executedToolResults[i];
                await _chatbotService.SaveMessagesAsync(customerId, new SaveChatbotMessageRequestDto
                {
                    SessionId = session.Id,
                    Role = nameof(ChatbotMessageRole.ASSISTANT),
                    ToolResult = new ChatbotToolResultRequestDto
                    {
                        ToolName = toolResult.ToolName,
                        ToolResult = toolResult.ToolResult
                    },
                    AllowSendToLockedSession = request.AllowSendToLockedSession
                }, cancellationToken);
            }
        }

        await _chatbotService.SaveMessagesAsync(customerId, new SaveChatbotMessageRequestDto
        {
            SessionId = session.Id,
            Role = nameof(ChatbotMessageRole.ASSISTANT),
            Content = assistantReply,
            IntentName = mergedIntent,
            SessionState = new ChatbotSessionStateUpdateRequestDto
            {
                CurrentIntent = mergedIntent,
                IntentState = updatedIntentState
            },
            AllowSendToLockedSession = request.AllowSendToLockedSession
        }, cancellationToken);

        if (shouldHandoff && handoffResult is not null)
        {
            await _chatbotService.MarkSessionHandedOffAsync(
                customerId,
                session.Id,
                handoffResult.ConversationId,
                handoffResult.SummaryJson,
                updatedIntentState,
                cancellationToken);
        }

        _logger.LogInformation(
            "Chatbot orchestration completed. CustomerId={CustomerId}, SessionId={SessionId}, Intent={Intent}, Confidence={Confidence:0.00}, Rejected={Rejected}, Handoff={Handoff}, RetrievalType={RetrievalType}, Grounded={Grounded}, Sources={SourceCount}",
            customerId,
            session.Id,
            mergedIntent,
            detection.Confidence,
            guardrail.IsRejected,
            shouldHandoff && handoffResult is not null,
            retrieval.QueryType,
            retrieval.IsGrounded,
            retrieval.Sources.Count);

        return new OrchestratedChatbotReplyDto
        {
            SessionId = session.Id,
            SessionToken = session.SessionToken,
            Intent = mergedIntent,
            Confidence = detection.Confidence,
            Strategy = fallbackTriggered ? "DETERMINISTIC_WITH_FALLBACK" : detection.Strategy,
            IsRejected = guardrail.IsRejected,
            RejectionReason = guardrail.RejectionMessage,
            ShouldHandoff = shouldHandoff && handoffResult is not null,
            AssistantReply = assistantReply,
            BookingWorkflow = bookingWorkflow,
            UpdatedIntentState = updatedIntentState,
            FunctionCalls = functionCalls,
            OrchestratedAt = DateTime.UtcNow
        };
    }

    private async Task<ChatbotSessionResponseDto?> ResolveSessionAsync(
        long customerId,
        OrchestrateChatbotRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId.GetValueOrDefault() > 0)
        {
            var byId = await _chatbotService.GetSessionByIdAsync(customerId, request.SessionId!.Value, cancellationToken);
            if (byId is not null)
            {
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SessionToken))
        {
            return await _chatbotService.GetSessionByTokenAsync(customerId, request.SessionToken.Trim(), cancellationToken);
        }

        return null;
    }

    private static string BuildAssistantReply(
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
            return guardrail.RejectionMessage ?? "Yêu c?u không h?p l?.";
        }

        if (detection.IsLowConfidence || intent == nameof(ChatbotIntentType.UNKNOWN))
        {
            return "Mình h? tr? trong ph?m vi garage: tu v?n l?i xe, báo giá, d?t l?ch, don d?ch v?, ph? tùng và b?o hành. B?n mô t? tình tr?ng xe ho?c nhu c?u c? th? nhé.";
        }

        return intent switch
        {
            nameof(ChatbotIntentType.CONSULTATION) => "Mình dã hi?u b?n c?n tu v?n. Mình s? h?i nhanh vài thông tin xe d? dua khuy?n ngh? phù h?p.",
            nameof(ChatbotIntentType.INQUIRY) => "Mình dã ghi nh?n câu h?i c?a b?n. Mình s? cung c?p thông tin chi ti?t theo yêu c?u.",
            nameof(ChatbotIntentType.PURCHASE) => "Mình dã ghi nh?n nhu c?u mua hàng/ph? tùng. Mình s? xác nh?n s?n ph?m và báo giá phù h?p.",
            nameof(ChatbotIntentType.SERVICE_ORDER) => "Mình dã nh?n yêu c?u liên quan don d?ch v?. Mình s? tra c?u tr?ng thái và thông tin liên quan.",
            nameof(ChatbotIntentType.BOOKING) => "Mình dã ghi nh?n yêu c?u d?t l?ch. Mình s? ti?p t?c thu th?p thông tin d? d? xu?t khung gi?.",
            nameof(ChatbotIntentType.HANDOFF) => "Mình dã nh?n thông tin. B?n vui lòng cung c?p thêm chi ti?t d? mình h? tr? chính xác hon.",
            _ => "Mình dã nh?n thông tin. B?n vui lòng cung c?p thêm chi ti?t d? mình h? tr? chính xác hon."
        };
    }

    private static string BuildUpdatedIntentState(
        string? currentStateJson,
        string intent,
        bool lowConfidence,
        bool shouldHandoff,
        string? violationCode,
        string sanitizedInput)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(currentStateJson ?? "{}") as JsonObject ?? new JsonObject();
        }
        catch
        {
            root = new JsonObject();
        }

        var flow = root["flow"] as JsonObject ?? new JsonObject();
        var previousIntent = flow["intent"]?.GetValue<string>();
        var previousStep = flow["step"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(previousIntent)
            && !string.Equals(previousIntent, intent, StringComparison.OrdinalIgnoreCase))
        {
            previousStep = null;
        }
        var nextStep = ResolveNextStep(intent, previousStep, sanitizedInput);
        var previousRevision = flow["revision"]?.GetValue<int>() ?? 0;

        flow["intent"] = intent;
        flow["step"] = nextStep;
        flow["revision"] = previousRevision + 1;
        flow["updated_at"] = DateTime.UtcNow;
        root["flow"] = flow;

        var flags = root["flags"] as JsonObject ?? new JsonObject();
        flags["is_low_confidence"] = lowConfidence;
        flags["needs_handoff"] = shouldHandoff;
        flags["violation_code"] = violationCode;
        root["flags"] = flags;

        return root.ToJsonString();
    }

    private static string ResolveNextStep(string intent, string? currentStep, string input)
    {
        var normalizedInput = input.Trim().ToLowerInvariant();
        var affirmative = normalizedInput.Contains("ok", StringComparison.OrdinalIgnoreCase)
            || normalizedInput.Contains("dong y", StringComparison.OrdinalIgnoreCase)
            || normalizedInput.Contains("xac nhan", StringComparison.OrdinalIgnoreCase);

        return intent switch
        {
            nameof(ChatbotIntentType.CONSULTATION) => currentStep switch
            {
                null => "collect_context",
                "collect_context" when affirmative => "provide_consultation",
                "provide_consultation" when affirmative => "confirm_follow_up",
                _ => currentStep ?? "collect_context"
            },
            nameof(ChatbotIntentType.INQUIRY) => currentStep switch
            {
                null => "identify_question",
                "identify_question" when affirmative => "provide_answer",
                _ => currentStep ?? "identify_question"
            },
            nameof(ChatbotIntentType.PURCHASE) => currentStep switch
            {
                null => "gather_requirements",
                "gather_requirements" when affirmative => "quote_or_offer",
                "quote_or_offer" when affirmative => "confirm_purchase",
                _ => currentStep ?? "gather_requirements"
            },
            nameof(ChatbotIntentType.SERVICE_ORDER) => currentStep switch
            {
                null => "gather_order_context",
                "gather_order_context" when affirmative => "prepare_service_order",
                "prepare_service_order" when affirmative => "confirm_service_order",
                _ => currentStep ?? "gather_order_context"
            },
            nameof(ChatbotIntentType.BOOKING) => currentStep switch
            {
                null => "gather_booking_info",
                "gather_booking_info" when affirmative => "suggest_slots",
                "suggest_slots" when affirmative => "confirm_booking",
                _ => currentStep ?? "gather_booking_info"
            },
            nameof(ChatbotIntentType.HANDOFF) => "clarify_intent",
            _ => "clarify_intent"
        };
    }

    private static string? BuildGarageGreetingReply(string normalizedInput)
    {
        if (!IsSimpleGreetingOnly(normalizedInput))
        {
            return null;
        }

        return "Xin chào b?n, b?n dã d?n v?i MyGarage. Mình có th? h? tr? các nhu c?u trong garage nhu tu v?n b?o du?ng/s?a ch?a, báo giá, d?t l?ch, ki?m tra don d?ch v?, ph? tùng và b?o hành. B?n c?n h? tr? gì hôm nay?";
    }

    private IReadOnlyList<ChatbotToolDefinition> GetLlmToolDefinitionsForIntent(string intent)
    {
        var definitions = _chatbotToolDefinitionRegistry
            .GetToolDefinitions()
            .Where(x => !LlmBlockedWriteTools.Contains(x.ToolName));

        var normalizedIntent = NormalizeIntent(intent);
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

    private static bool IsSimpleGreetingOnly(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        var hasGreetingPrefix = normalizedInput.StartsWith("hi", StringComparison.Ordinal)
            || normalizedInput.StartsWith("hello", StringComparison.Ordinal)
            || normalizedInput.StartsWith("xin chao", StringComparison.Ordinal)
            || normalizedInput.StartsWith("xin chào", StringComparison.Ordinal)
            || normalizedInput.StartsWith("chao", StringComparison.Ordinal)
            || normalizedInput.StartsWith("chào", StringComparison.Ordinal)
            || normalizedInput.StartsWith("alo", StringComparison.Ordinal);
        if (!hasGreetingPrefix)
        {
            return false;
        }

        var hasTransactionalSignal =
            normalizedInput.Contains("d?t l?ch", StringComparison.Ordinal)
            || normalizedInput.Contains("dat lich", StringComparison.Ordinal)
            || normalizedInput.Contains("bao gia", StringComparison.Ordinal)
            || normalizedInput.Contains("báo giá", StringComparison.Ordinal)
            || normalizedInput.Contains("thay dau", StringComparison.Ordinal)
            || normalizedInput.Contains("thay d?u", StringComparison.Ordinal)
            || normalizedInput.Contains("dich vu", StringComparison.Ordinal)
            || normalizedInput.Contains("d?ch v?", StringComparison.Ordinal);
        if (hasTransactionalSignal)
        {
            return false;
        }

        var tokenCount = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return tokenCount <= 6;
    }

    private static string BuildLlmGatewayFailureReply(ChatbotLlmGatewayResponse? response)
    {
        if (response is null)
        {
            return "Hi?n t?i AI Gemini chua ph?n h?i. Vui lòng th? l?i sau ít phút.";
        }

        if (string.Equals(response.FailureCode, "PROVIDER_API_KEY_MISSING", StringComparison.OrdinalIgnoreCase))
        {
            return "Tính nang AI dang t?m th?i gián do?n. Vui lòng th? l?i sau ít phút ho?c ch?n g?p tu v?n viên.";
        }

        if (string.Equals(response.FailureCode, "MODEL_CONFIG_NOT_FOUND", StringComparison.OrdinalIgnoreCase))
        {
            return "Tính nang AI dang t?m th?i gián do?n. Vui lòng th? l?i sau ít phút ho?c ch?n g?p tu v?n viên.";
        }

        if (string.Equals(response.FailureCode, "PROVIDER_ENDPOINT_NOT_CONFIGURED", StringComparison.OrdinalIgnoreCase))
        {
            return "Tính nang AI dang t?m th?i gián do?n. Vui lòng th? l?i sau ít phút ho?c ch?n g?p tu v?n viên.";
        }

        return $"AI Gemini t?m th?i chua ph?n h?i ({response.FailureCode}). Vui lòng th? l?i sau.";
    }

    private static bool ShouldUseDeterministicRetrievalReply(ChatbotRetrievalResult retrieval)
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

    private static string RemoveBookingStateWhenLeavingFlow(string intentStateJson, string intent)
    {
        if (string.Equals(intent, nameof(ChatbotIntentType.BOOKING), StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(intentStateJson))
        {
            return intentStateJson;
        }

        try
        {
            var root = JsonNode.Parse(intentStateJson) as JsonObject;
            if (root is null)
            {
                return intentStateJson;
            }

            root.Remove("booking");
            return root.ToJsonString();
        }
        catch
        {
            return intentStateJson;
        }
    }

    private static string NormalizeIntent(string? intent)
    {
        return string.IsNullOrWhiteSpace(intent)
            ? nameof(ChatbotIntentType.UNKNOWN)
            : intent.Trim().ToUpperInvariant();
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

    private static string NormalizeForRouting(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return input.Trim().ToLowerInvariant();
    }

    private static string NormalizeForSignalMatching(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var lowered = input.Trim().ToLowerInvariant().Replace('\u0111', 'd').Replace('\u0110', 'd');
        var decomposed = lowered.Normalize(System.Text.NormalizationForm.FormD);
        var buffer = new List<char>(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is System.Globalization.UnicodeCategory.NonSpacingMark
                or System.Globalization.UnicodeCategory.SpacingCombiningMark
                or System.Globalization.UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            buffer.Add(char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ');
        }

        return string.Join(' ', new string(buffer.ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string BuildContextualUserInput(
        string userInput,
        IReadOnlyList<ChatbotMessageResponseDto> history)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return userInput;
        }

        var normalizedInput = NormalizeForSignalMatching(userInput);
        if (HasPricingSignal(normalizedInput))
        {
            return userInput;
        }

        if (!IsShortCategoryReply(normalizedInput))
        {
            return userInput;
        }

        var lastAssistantContent = history?
            .Where(x =>
                x is not null &&
                string.Equals(x.Role, nameof(ChatbotMessageRole.ASSISTANT), StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(x.Content))
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Content!)
            .LastOrDefault();

        var lastUserContent = history?
            .Where(x =>
                x is not null &&
                string.Equals(x.Role, nameof(ChatbotMessageRole.USER), StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(x.Content))
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Content!)
            .LastOrDefault();

        var assistantAskedPriceFollowUp = !string.IsNullOrWhiteSpace(lastAssistantContent) &&
            IsPricingFollowUpPrompt(NormalizeForRouting(lastAssistantContent));
        var assistantIsBookingStepPrompt = !string.IsNullOrWhiteSpace(lastAssistantContent) &&
            IsBookingStepPrompt(NormalizeForRouting(lastAssistantContent));
        var previousUserWasPricing = !string.IsNullOrWhiteSpace(lastUserContent) &&
            HasPricingSignal(NormalizeForRouting(lastUserContent));

        if (assistantIsBookingStepPrompt)
        {
            return userInput;
        }

        if (assistantAskedPriceFollowUp || previousUserWasPricing)
        {
            return $"báo giá {userInput}".Trim();
        }

        return userInput;
    }

    private static string? TryBuildBookingOfferHandoffPrefill(
        string userInput,
        IReadOnlyList<ChatbotMessageResponseDto> history)
    {
        var normalizedInput = NormalizeForSignalMatching(userInput);
        if (!IsAffirmativeBookingAcceptance(normalizedInput))
        {
            return null;
        }

        var lastAssistantContent = history?
            .Where(x =>
                x is not null &&
                string.Equals(x.Role, nameof(ChatbotMessageRole.ASSISTANT), StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(x.Content))
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Content!.Trim())
            .LastOrDefault();
        if (string.IsNullOrWhiteSpace(lastAssistantContent))
        {
            return null;
        }

        if (!IsBookingOfferPrompt(lastAssistantContent))
        {
            return null;
        }

        var recentUserContext = FindLatestBookingContextUserMessage(history);
        var serviceHint = TryExtractSuggestedServiceHint(lastAssistantContent);

        var segments = new List<string>();
        if (!string.IsNullOrWhiteSpace(recentUserContext))
        {
            segments.Add(recentUserContext.Trim());
        }

        if (segments.Count == 0 || !segments.Any(segment => HasDirectBookingSignal(segment)))
        {
            segments.Insert(0, "dat lich");
        }

        if (!string.IsNullOrWhiteSpace(serviceHint)
            && !segments.Any(segment =>
                NormalizeForSignalMatching(segment).Contains(NormalizeForSignalMatching(serviceHint), StringComparison.Ordinal)))
        {
            segments.Add(serviceHint.Trim());
        }

        var prefill = string.Join(" ", segments.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)).Trim();
        return string.IsNullOrWhiteSpace(prefill) ? null : prefill;
    }

    private static string SeedBookingStateFromOfferHandoff(string intentStateJson, string bookingPrefillInput)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(intentStateJson) as JsonObject ?? new JsonObject();
        }
        catch
        {
            root = new JsonObject();
        }

        var flow = root["flow"] as JsonObject ?? new JsonObject();
        flow["intent"] = nameof(ChatbotIntentType.BOOKING);
        flow["step"] = "gather_booking_info";
        flow["updated_at"] = DateTime.UtcNow;
        root["flow"] = flow;

        var booking = root["booking"] as JsonObject ?? new JsonObject();
        booking["Step"] = 0;
        booking["PrefillInput"] = bookingPrefillInput;
        booking["AutoConfirmRequested"] = true;
        root["booking"] = booking;

        return root.ToJsonString();
    }

    private static bool IsShortCategoryReply(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        var tokenCount = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return tokenCount is >= 1 and <= 4;
    }

    private static bool IsPricingFollowUpPrompt(string normalizedAssistantReply)
    {
        if (string.IsNullOrWhiteSpace(normalizedAssistantReply))
        {
            return false;
        }

        var asksGroupSelection = normalizedAssistantReply.Contains("nhóm nào", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("nhom nao", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("d?ch v? nào", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("dich vu nao", StringComparison.Ordinal);
        var hasPricingContext = HasPricingSignal(normalizedAssistantReply)
            || normalizedAssistantReply.Contains("g?n dúng", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("gan dung", StringComparison.Ordinal);

        return asksGroupSelection && hasPricingContext;
    }

    private static bool IsBookingStepPrompt(string normalizedAssistantReply)
    {
        if (string.IsNullOrWhiteSpace(normalizedAssistantReply))
        {
            return false;
        }

        return normalizedAssistantReply.Contains("buoc 1/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("bu?c 1/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("buoc 2/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("bu?c 2/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("buoc 3/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("bu?c 3/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("buoc 4/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("bu?c 4/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("buoc 5/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("bu?c 5/5", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("chon xe", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("ch?n xe", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("chon nhom dich vu", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("ch?n nhóm d?ch v?", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("chon dich vu chi tiet", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("ch?n d?ch v? chi ti?t", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("chon chi nhanh", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("ch?n chi nhánh", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("chon khung gio", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("ch?n khung gi?", StringComparison.Ordinal);
    }

    private static bool IsBookingOfferPrompt(string normalizedAssistantReply)
    {
        normalizedAssistantReply = NormalizeForSignalMatching(normalizedAssistantReply);
        if (string.IsNullOrWhiteSpace(normalizedAssistantReply))
        {
            return false;
        }

        var asksToBook = normalizedAssistantReply.Contains("ban co muon dat lich", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("muon dat lich cho dich vu nay", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("co muon hen lich", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("dat lich hen", StringComparison.Ordinal);
        if (!asksToBook)
        {
            return false;
        }

        return normalizedAssistantReply.Contains("dich vu", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("bao duong", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("sua chua", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("dieu hoa", StringComparison.Ordinal)
            || normalizedAssistantReply.Contains("kiem tra", StringComparison.Ordinal);
    }

    private static bool IsAffirmativeBookingAcceptance(string normalizedInput)
    {
        normalizedInput = NormalizeForSignalMatching(normalizedInput);
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        return normalizedInput.Equals("ok", StringComparison.Ordinal)
            || normalizedInput.Equals("oke", StringComparison.Ordinal)
            || normalizedInput.Equals("okay", StringComparison.Ordinal)
            || normalizedInput.Equals("yes", StringComparison.Ordinal)
            || normalizedInput.Equals("dong y", StringComparison.Ordinal)
            || normalizedInput.Equals("duoc", StringComparison.Ordinal)
            || normalizedInput.Equals("vang", StringComparison.Ordinal)
            || normalizedInput.Contains("dat lich", StringComparison.Ordinal)
            || normalizedInput.Contains("hen lich", StringComparison.Ordinal);
    }

    private static string? FindLatestBookingContextUserMessage(IReadOnlyList<ChatbotMessageResponseDto>? history)
    {
        return history?
            .Where(x =>
                x is not null &&
                string.Equals(x.Role, nameof(ChatbotMessageRole.USER), StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(x.Content))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Content!.Trim())
            .FirstOrDefault(HasBookingContextSignal);
    }

    private static bool HasBookingContextSignal(string normalizedMessage)
    {
        normalizedMessage = NormalizeForSignalMatching(normalizedMessage);
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return false;
        }

        if (HasDirectBookingSignal(normalizedMessage))
        {
            return true;
        }

        return normalizedMessage.Contains("ngay mai", StringComparison.Ordinal)
            || normalizedMessage.Contains("hom nay", StringComparison.Ordinal)
            || normalizedMessage.Contains("gio", StringComparison.Ordinal)
            || normalizedMessage.Contains("chi nhanh", StringComparison.Ordinal)
            || normalizedMessage.Contains("xe", StringComparison.Ordinal)
            || normalizedMessage.Contains("bien so", StringComparison.Ordinal)
            || normalizedMessage.Contains("slot", StringComparison.Ordinal)
            || Regex.IsMatch(normalizedMessage, @"\b\d{1,2}\s*h\b", RegexOptions.IgnoreCase);
    }

    private static bool HasDirectBookingSignal(string normalizedMessage)
    {
        normalizedMessage = NormalizeForSignalMatching(normalizedMessage);
        return normalizedMessage.Contains("dat lich", StringComparison.Ordinal)
            || normalizedMessage.Contains("hen lich", StringComparison.Ordinal)
            || normalizedMessage.Contains("book", StringComparison.Ordinal)
            || normalizedMessage.Contains("appointment", StringComparison.Ordinal);
    }

    private static string? TryExtractSuggestedServiceHint(string assistantReply)
    {
        if (string.IsNullOrWhiteSpace(assistantReply))
        {
            return null;
        }

        foreach (Match match in MarkdownBoldRegex.Matches(assistantReply))
        {
            var value = match.Groups["value"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        foreach (Match match in MarkdownCodeRegex.Matches(assistantReply))
        {
            var value = match.Groups["value"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        var serviceMatch = ServiceOfferRegex.Match(assistantReply);
        if (serviceMatch.Success)
        {
            var value = serviceMatch.Groups["value"].Value
                .Replace("**", string.Empty, StringComparison.Ordinal)
                .Replace("`", string.Empty, StringComparison.Ordinal)
                .Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private static bool HasPricingSignal(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        return normalizedInput.Contains("báo giá", StringComparison.Ordinal)
            || normalizedInput.Contains("bao gia", StringComparison.Ordinal)
            || normalizedInput.Contains("b?ng giá", StringComparison.Ordinal)
            || normalizedInput.Contains("bang gia", StringComparison.Ordinal)
            || normalizedInput.Contains("price", StringComparison.Ordinal)
            || normalizedInput.Contains("quote", StringComparison.Ordinal)
            || normalizedInput.Contains("quotation", StringComparison.Ordinal)
            || normalizedInput.Contains("chi phí", StringComparison.Ordinal)
            || normalizedInput.Contains("chi phi", StringComparison.Ordinal);
    }

    private static bool IsDirectBookingRequest(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        var asksBooking = normalizedInput.Contains("d?t l?ch", StringComparison.Ordinal)
            || normalizedInput.Contains("dat lich", StringComparison.Ordinal)
            || normalizedInput.Contains("book", StringComparison.Ordinal)
            || normalizedInput.Contains("appointment", StringComparison.Ordinal);
        if (!asksBooking)
        {
            return false;
        }

        return normalizedInput.Contains("giúp tôi", StringComparison.Ordinal)
            || normalizedInput.Contains("giup toi", StringComparison.Ordinal)
            || normalizedInput.Contains("giúp em", StringComparison.Ordinal)
            || normalizedInput.Contains("giup em", StringComparison.Ordinal)
            || normalizedInput.Contains("h? tôi", StringComparison.Ordinal)
            || normalizedInput.Contains("ho toi", StringComparison.Ordinal)
            || normalizedInput.Contains("h? em", StringComparison.Ordinal)
            || normalizedInput.Contains("ho em", StringComparison.Ordinal)
            || normalizedInput.Contains("book for me", StringComparison.Ordinal)
            || normalizedInput.Contains("t? d?ng", StringComparison.Ordinal)
            || normalizedInput.Contains("tu dong", StringComparison.Ordinal)
            || normalizedInput.Contains("luôn", StringComparison.Ordinal)
            || normalizedInput.Contains("luon", StringComparison.Ordinal)
            || normalizedInput.Contains("ngay", StringComparison.Ordinal);
    }

    private static bool ShouldContinueBookingFlow(
        string? intentStateJson,
        string? currentIntent,
        string userInput)
    {
        if (IsBookingExitCommand(userInput))
        {
            return false;
        }

        if (!IsBookingInProgress(intentStateJson, currentIntent))
        {
            return false;
        }

        var normalizedInput = NormalizeForSignalMatching(userInput);
        if (IsBookingContextSwitchCommand(normalizedInput))
        {
            return false;
        }

        return IsBookingContinuationInput(normalizedInput);
    }

    private static bool IsBookingInProgress(string? intentStateJson, string? currentIntent)
    {
        if (string.IsNullOrWhiteSpace(intentStateJson))
        {
            return string.Equals(currentIntent, nameof(ChatbotIntentType.BOOKING), StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            var root = JsonNode.Parse(intentStateJson) as JsonObject;
            if (root is null)
            {
                return false;
            }

            var flowIntent = root["flow"]?["intent"]?.GetValue<string>();
            if (string.Equals(flowIntent, nameof(ChatbotIntentType.BOOKING), StringComparison.OrdinalIgnoreCase))
            {
                return !IsBookingCompleted(intentStateJson);
            }

            if (string.IsNullOrWhiteSpace(flowIntent)
                && string.Equals(currentIntent, nameof(ChatbotIntentType.BOOKING), StringComparison.OrdinalIgnoreCase))
            {
                return root["booking"] is JsonObject && !IsBookingCompleted(intentStateJson);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsBookingCompleted(string? intentStateJson)
    {
        if (string.IsNullOrWhiteSpace(intentStateJson))
        {
            return false;
        }

        try
        {
            var root = JsonNode.Parse(intentStateJson) as JsonObject;
            var bookingNode = root?["booking"] as JsonObject;
            if (bookingNode is null)
            {
                return false;
            }

            var step = bookingNode["Step"]?.GetValue<string>()
                ?? bookingNode["step"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(step))
            {
                return step.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                    || step.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase);
            }

            var numericStep = bookingNode["Step"]?.GetValue<int?>()
                ?? bookingNode["step"]?.GetValue<int?>();
            return numericStep.HasValue && numericStep.Value >= 5;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsBookingExitCommand(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return false;
        }

        var normalized = userInput.Trim().ToLowerInvariant();
        return normalized.Contains("huy dat lich", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("h?y d?t l?ch", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("khong dat lich", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("không d?t l?ch", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("huy", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("h?y", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("thoat", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("thoát", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("cancel", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBookingContextSwitchCommand(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        var switchedIntent = ResolveContextSwitchIntent(normalizedInput);
        if (!string.Equals(switchedIntent, nameof(ChatbotIntentType.UNKNOWN), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(switchedIntent, nameof(ChatbotIntentType.BOOKING), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalizedInput.StartsWith("hi", StringComparison.Ordinal)
            || normalizedInput.StartsWith("hello", StringComparison.Ordinal)
            || normalizedInput.StartsWith("xin chao", StringComparison.Ordinal)
            || normalizedInput.StartsWith("xin chào", StringComparison.Ordinal)
            || normalizedInput.StartsWith("chao", StringComparison.Ordinal)
            || normalizedInput.StartsWith("chào", StringComparison.Ordinal);
    }

    private static bool IsExplicitHandoffRequest(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return false;
        }

        return normalizedInput.Contains("nhân viên", StringComparison.Ordinal)
            || normalizedInput.Contains("nhan vien", StringComparison.Ordinal)
            || normalizedInput.Contains("ngu?i th?t", StringComparison.Ordinal)
            || normalizedInput.Contains("nguoi that", StringComparison.Ordinal)
            || normalizedInput.Contains("tu v?n viên", StringComparison.Ordinal)
            || normalizedInput.Contains("tu van vien", StringComparison.Ordinal)
            || normalizedInput.Contains("g?i t?ng dài", StringComparison.Ordinal)
            || normalizedInput.Contains("goi tong dai", StringComparison.Ordinal)
            || normalizedInput.Contains("handoff", StringComparison.Ordinal)
            || normalizedInput.Contains("support agent", StringComparison.Ordinal)
            || normalizedInput.Contains("human agent", StringComparison.Ordinal);
    }

    private static bool IsBookingContinuationInput(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return true;
        }

        var hasBookingSignal = normalizedInput.Contains("d?t l?ch", StringComparison.Ordinal)
            || normalizedInput.Contains("dat lich", StringComparison.Ordinal)
            || normalizedInput.Contains("book", StringComparison.Ordinal)
            || normalizedInput.Contains("appointment", StringComparison.Ordinal)
            || normalizedInput.Contains("slot", StringComparison.Ordinal)
            || normalizedInput.Contains("khung gio", StringComparison.Ordinal)
            || normalizedInput.Contains("khung gi?", StringComparison.Ordinal)
            || normalizedInput.Contains("vehicle", StringComparison.Ordinal)
            || normalizedInput.Contains("bien so", StringComparison.Ordinal)
            || normalizedInput.Contains("bi?n s?", StringComparison.Ordinal)
            || normalizedInput.Contains("chi nhanh", StringComparison.Ordinal)
            || normalizedInput.Contains("chi nhánh", StringComparison.Ordinal)
            || normalizedInput.Contains("dich vu", StringComparison.Ordinal)
            || normalizedInput.Contains("d?ch v?", StringComparison.Ordinal)
            || normalizedInput.Contains("model", StringComparison.Ordinal)
            || normalizedInput.Contains("vin", StringComparison.Ordinal)
            || normalizedInput.Contains("yes", StringComparison.Ordinal)
            || normalizedInput.Contains("ok", StringComparison.Ordinal)
            || normalizedInput.Contains("oke", StringComparison.Ordinal)
            || normalizedInput.Contains("dong y", StringComparison.Ordinal)
            || normalizedInput.Contains("d?ng ý", StringComparison.Ordinal)
            || normalizedInput.Contains("xac nhan", StringComparison.Ordinal)
            || normalizedInput.Contains("xác nh?n", StringComparison.Ordinal)
            || normalizedInput.Contains("hom nay", StringComparison.Ordinal)
            || normalizedInput.Contains("hôm nay", StringComparison.Ordinal)
            || normalizedInput.Contains("ngay mai", StringComparison.Ordinal)
            || normalizedInput.Contains("ngày mai", StringComparison.Ordinal);
        if (hasBookingSignal)
        {
            return true;
        }

        if (normalizedInput.Any(char.IsDigit))
        {
            return true;
        }

        var tokenCount = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return tokenCount <= 2;
    }

    private static string ResolveContextSwitchIntent(string normalizedInput)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return nameof(ChatbotIntentType.UNKNOWN);
        }

        var handoffSignal = normalizedInput.Contains("nhan vien", StringComparison.Ordinal)
            || normalizedInput.Contains("nhân viên", StringComparison.Ordinal)
            || normalizedInput.Contains("nguoi that", StringComparison.Ordinal)
            || normalizedInput.Contains("ngu?i th?t", StringComparison.Ordinal)
            || normalizedInput.Contains("support agent", StringComparison.Ordinal)
            || normalizedInput.Contains("human agent", StringComparison.Ordinal);
        if (handoffSignal)
        {
            return nameof(ChatbotIntentType.HANDOFF);
        }

        var purchaseSignal = normalizedInput.Contains("mua hang", StringComparison.Ordinal)
            || normalizedInput.Contains("mua hàng", StringComparison.Ordinal)
            || normalizedInput.Contains("mua do", StringComparison.Ordinal)
            || normalizedInput.Contains("mua d?", StringComparison.Ordinal)
            || normalizedInput.Contains("phu tung", StringComparison.Ordinal)
            || normalizedInput.Contains("ph? tùng", StringComparison.Ordinal)
            || normalizedInput.Contains("linh kien", StringComparison.Ordinal)
            || normalizedInput.Contains("linh ki?n", StringComparison.Ordinal)
            || normalizedInput.Contains("san pham", StringComparison.Ordinal)
            || normalizedInput.Contains("s?n ph?m", StringComparison.Ordinal)
            || normalizedInput.Contains("retail", StringComparison.Ordinal)
            || normalizedInput.Contains("don hang", StringComparison.Ordinal)
            || normalizedInput.Contains("don hàng", StringComparison.Ordinal);
        if (purchaseSignal)
        {
            return nameof(ChatbotIntentType.PURCHASE);
        }

        var serviceOrderSignal = normalizedInput.Contains("don dich vu", StringComparison.Ordinal)
            || normalizedInput.Contains("don d?ch v?", StringComparison.Ordinal)
            || normalizedInput.Contains("service order", StringComparison.Ordinal)
            || normalizedInput.Contains("tien do", StringComparison.Ordinal)
            || normalizedInput.Contains("ti?n d?", StringComparison.Ordinal)
            || normalizedInput.Contains("trang thai don", StringComparison.Ordinal)
            || normalizedInput.Contains("tr?ng thái don", StringComparison.Ordinal)
            || normalizedInput.Contains("bao hanh", StringComparison.Ordinal)
            || normalizedInput.Contains("b?o hành", StringComparison.Ordinal)
            || normalizedInput.Contains("hoa don", StringComparison.Ordinal)
            || normalizedInput.Contains("hóa don", StringComparison.Ordinal)
            || normalizedInput.Contains("thanh toan", StringComparison.Ordinal)
            || normalizedInput.Contains("thanh toán", StringComparison.Ordinal);
        if (serviceOrderSignal)
        {
            return nameof(ChatbotIntentType.SERVICE_ORDER);
        }

        var consultationSignal = normalizedInput.Contains("tu van", StringComparison.Ordinal)
            || normalizedInput.Contains("tu v?n", StringComparison.Ordinal)
            || normalizedInput.Contains("consult", StringComparison.Ordinal)
            || normalizedInput.Contains("advice", StringComparison.Ordinal);
        if (consultationSignal)
        {
            return nameof(ChatbotIntentType.CONSULTATION);
        }

        var inquirySignal = normalizedInput.Contains("hoi", StringComparison.Ordinal)
            || normalizedInput.Contains("h?i", StringComparison.Ordinal)
            || normalizedInput.Contains("thong tin", StringComparison.Ordinal)
            || normalizedInput.Contains("thông tin", StringComparison.Ordinal)
            || normalizedInput.Contains("bao gia", StringComparison.Ordinal)
            || normalizedInput.Contains("báo giá", StringComparison.Ordinal);
        if (inquirySignal)
        {
            return nameof(ChatbotIntentType.INQUIRY);
        }

        var greetingSignal = normalizedInput.StartsWith("hi", StringComparison.Ordinal)
            || normalizedInput.StartsWith("hello", StringComparison.Ordinal)
            || normalizedInput.StartsWith("xin chao", StringComparison.Ordinal)
            || normalizedInput.StartsWith("xin chào", StringComparison.Ordinal)
            || normalizedInput.StartsWith("chao", StringComparison.Ordinal)
            || normalizedInput.StartsWith("chào", StringComparison.Ordinal);
        if (greetingSignal)
        {
            return nameof(ChatbotIntentType.CONSULTATION);
        }

        return nameof(ChatbotIntentType.UNKNOWN);
    }
}















