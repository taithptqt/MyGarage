using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotIntentRoutingService
{
    ChatbotIntentRoutingResult ResolveIntentAndState(
        string contextualUserInput,
        string normalizedUserInput,
        string? bookingOfferHandoffPrefill,
        string? currentIntent,
        string? currentIntentStateJson,
        GuardrailValidationResult guardrail);
}

public sealed class ChatbotIntentRoutingResult
{
    public IntentDetectionResult Detection { get; set; } = new();

    public string MergedIntent { get; set; } = string.Empty;

    public bool ShouldHandoff { get; set; }

    public bool FallbackTriggered { get; set; }

    public string UpdatedIntentState { get; set; } = "{}";
}
