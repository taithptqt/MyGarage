using System;
using System.Linq;
using System.Text.Json.Nodes;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotIntentRoutingService : IChatbotIntentRoutingService
{
    private readonly IIntentDetector _intentDetector;
    private readonly IChatbotTurnContextService _turnContextService;

    public ChatbotIntentRoutingService(
        IIntentDetector intentDetector,
        IChatbotTurnContextService turnContextService)
    {
        _intentDetector = intentDetector;
        _turnContextService = turnContextService;
    }

    public ChatbotIntentRoutingResult ResolveIntentAndState(
        string contextualUserInput,
        string normalizedUserInput,
        string? bookingOfferHandoffPrefill,
        string? currentIntent,
        string? currentIntentStateJson,
        GuardrailValidationResult guardrail)
    {
        var detection = _intentDetector.Detect(contextualUserInput, currentIntentStateJson);

        if (IsDirectBookingRequest(normalizedUserInput))
        {
            ApplyDetectionOverride(
                detection,
                nameof(ChatbotIntentType.BOOKING),
                0.85m,
                "DIRECT_BOOKING_REQUEST",
                "User explicitly requested automatic booking.");
        }

        if (!string.IsNullOrWhiteSpace(bookingOfferHandoffPrefill))
        {
            ApplyDetectionOverride(
                detection,
                nameof(ChatbotIntentType.BOOKING),
                0.88m,
                "CONSULTATION_BOOKING_HANDOFF",
                "User accepted a prior booking offer and the session was promoted into booking.");
        }

        var bookingInProgress = IsBookingInProgress(currentIntentStateJson, currentIntent);
        var bookingExitRequested = IsBookingExitCommand(contextualUserInput);
        var bookingContextSwitchRequested = IsBookingContextSwitchCommand(normalizedUserInput);
        var bookingContextSwitchIntent = bookingContextSwitchRequested
            ? ResolveContextSwitchIntent(normalizedUserInput)
            : nameof(ChatbotIntentType.UNKNOWN);
        var continueBooking = ShouldContinueBookingFlow(
            currentIntentStateJson,
            currentIntent,
            contextualUserInput);

        if (bookingInProgress && bookingContextSwitchRequested)
        {
            ApplyDetectionOverride(
                detection,
                bookingContextSwitchIntent,
                0.80m,
                "BOOKING_CONTEXT_SWITCH",
                "User switched away from booking context.");
        }
        else if (bookingInProgress && !bookingExitRequested)
        {
            ApplyDetectionOverride(
                detection,
                nameof(ChatbotIntentType.BOOKING),
                0.90m,
                "BOOKING_INTENT_LOCK",
                "Booking flow is active and locked until user exits explicitly.");
        }
        else if (continueBooking)
        {
            ApplyDetectionOverride(
                detection,
                nameof(ChatbotIntentType.BOOKING),
                0.75m,
                "STATEFUL_BOOKING_CONTINUATION",
                "Booking flow is in progress in session state.");
        }
        else if (bookingContextSwitchRequested
            && string.Equals(detection.Intent, nameof(ChatbotIntentType.BOOKING), StringComparison.OrdinalIgnoreCase))
        {
            ApplyDetectionOverride(
                detection,
                bookingContextSwitchIntent,
                0.80m,
                "BOOKING_CONTEXT_SWITCH",
                "User switched away from booking context.");
        }

        var mergedIntent = NormalizeIntent(detection.Intent);
        var shouldHandoff = guardrail.ShouldHandoff
            || string.Equals(mergedIntent, nameof(ChatbotIntentType.HANDOFF), StringComparison.OrdinalIgnoreCase)
            || IsExplicitHandoffRequest(normalizedUserInput);
        if (shouldHandoff)
        {
            mergedIntent = nameof(ChatbotIntentType.HANDOFF);
            ApplyDetectionOverride(
                detection,
                nameof(ChatbotIntentType.HANDOFF),
                0.90m,
                "EXPLICIT_HUMAN_HANDOFF",
                "User requested a human agent handoff.");
        }

        var fallbackTriggered = detection.IsLowConfidence
            && mergedIntent == nameof(ChatbotIntentType.UNKNOWN)
            && !shouldHandoff;
        var updatedIntentState = BuildUpdatedIntentState(
            currentIntentStateJson,
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

        return new ChatbotIntentRoutingResult
        {
            Detection = detection,
            MergedIntent = mergedIntent,
            ShouldHandoff = shouldHandoff,
            FallbackTriggered = fallbackTriggered,
            UpdatedIntentState = updatedIntentState
        };
    }

    private static void ApplyDetectionOverride(
        IntentDetectionResult detection,
        string intent,
        decimal minConfidence,
        string strategy,
        string explanation)
    {
        detection.Intent = intent;
        detection.IsLowConfidence = false;
        detection.Confidence = Math.Max(detection.Confidence, minConfidence);
        detection.Strategy = strategy;
        detection.Explanation = explanation;
    }

    private static string NormalizeIntent(string? intent)
    {
        return string.IsNullOrWhiteSpace(intent)
            ? nameof(ChatbotIntentType.UNKNOWN)
            : intent.Trim().ToUpperInvariant();
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

    private bool ShouldContinueBookingFlow(
        string? intentStateJson,
        string? currentIntent,
        string userInput)
    {
        if (IsBookingExitCommand(userInput) || !IsBookingInProgress(intentStateJson, currentIntent))
        {
            return false;
        }

        var normalizedInput = _turnContextService.NormalizeForSignalMatching(userInput);
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
        if (hasBookingSignal || normalizedInput.Any(char.IsDigit))
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

        if (normalizedInput.Contains("nhan vien", StringComparison.Ordinal)
            || normalizedInput.Contains("nhân viên", StringComparison.Ordinal)
            || normalizedInput.Contains("nguoi that", StringComparison.Ordinal)
            || normalizedInput.Contains("ngu?i th?t", StringComparison.Ordinal)
            || normalizedInput.Contains("support agent", StringComparison.Ordinal)
            || normalizedInput.Contains("human agent", StringComparison.Ordinal))
        {
            return nameof(ChatbotIntentType.HANDOFF);
        }

        if (normalizedInput.Contains("mua hang", StringComparison.Ordinal)
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
            || normalizedInput.Contains("don hàng", StringComparison.Ordinal))
        {
            return nameof(ChatbotIntentType.PURCHASE);
        }

        if (normalizedInput.Contains("don dich vu", StringComparison.Ordinal)
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
            || normalizedInput.Contains("thanh toán", StringComparison.Ordinal))
        {
            return nameof(ChatbotIntentType.SERVICE_ORDER);
        }

        if (normalizedInput.Contains("tu van", StringComparison.Ordinal)
            || normalizedInput.Contains("tu v?n", StringComparison.Ordinal)
            || normalizedInput.Contains("consult", StringComparison.Ordinal)
            || normalizedInput.Contains("advice", StringComparison.Ordinal))
        {
            return nameof(ChatbotIntentType.CONSULTATION);
        }

        if (normalizedInput.Contains("hoi", StringComparison.Ordinal)
            || normalizedInput.Contains("h?i", StringComparison.Ordinal)
            || normalizedInput.Contains("thong tin", StringComparison.Ordinal)
            || normalizedInput.Contains("thông tin", StringComparison.Ordinal)
            || normalizedInput.Contains("bao gia", StringComparison.Ordinal)
            || normalizedInput.Contains("báo giá", StringComparison.Ordinal))
        {
            return nameof(ChatbotIntentType.INQUIRY);
        }

        if (normalizedInput.StartsWith("hi", StringComparison.Ordinal)
            || normalizedInput.StartsWith("hello", StringComparison.Ordinal)
            || normalizedInput.StartsWith("xin chao", StringComparison.Ordinal)
            || normalizedInput.StartsWith("xin chào", StringComparison.Ordinal)
            || normalizedInput.StartsWith("chao", StringComparison.Ordinal)
            || normalizedInput.StartsWith("chào", StringComparison.Ordinal))
        {
            return nameof(ChatbotIntentType.CONSULTATION);
        }

        return nameof(ChatbotIntentType.UNKNOWN);
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
}
