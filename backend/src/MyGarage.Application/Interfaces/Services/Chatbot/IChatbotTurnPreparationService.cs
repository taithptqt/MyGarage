using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotTurnPreparationService
{
    ChatbotTurnPreparationResult PrepareTurn(
        string userMessage,
        ChatbotSessionResponseDto session);
}

public sealed class ChatbotTurnPreparationResult
{
    public GuardrailValidationResult Guardrail { get; set; } = new();

    public string ContextualUserInput { get; set; } = string.Empty;

    public string? ScopedGreetingReply { get; set; }

    public ChatbotIntentRoutingResult IntentRouting { get; set; } = new();

    public PromptBuildResult Prompt { get; set; } = new();
}
