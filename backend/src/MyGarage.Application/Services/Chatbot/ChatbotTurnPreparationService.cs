using System;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotTurnPreparationService : IChatbotTurnPreparationService
{
    private readonly IGuardrailValidator _guardrailValidator;
    private readonly IChatbotTurnContextService _turnContextService;
    private readonly IChatbotIntentRoutingService _intentRoutingService;
    private readonly IPromptBuilder _promptBuilder;

    public ChatbotTurnPreparationService(
        IGuardrailValidator guardrailValidator,
        IChatbotTurnContextService turnContextService,
        IChatbotIntentRoutingService intentRoutingService,
        IPromptBuilder promptBuilder)
    {
        _guardrailValidator = guardrailValidator;
        _turnContextService = turnContextService;
        _intentRoutingService = intentRoutingService;
        _promptBuilder = promptBuilder;
    }

    public ChatbotTurnPreparationResult PrepareTurn(
        string userMessage,
        ChatbotSessionResponseDto session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var guardrail = _guardrailValidator.Validate(userMessage);
        var contextualUserInput = _turnContextService.BuildContextualUserInput(
            guardrail.SanitizedInput,
            session.Messages);
        var normalizedUserInput = _turnContextService.NormalizeForRouting(contextualUserInput);
        var scopedGreetingReply = _turnContextService.BuildGarageGreetingReply(normalizedUserInput);
        var bookingOfferHandoffPrefill = _turnContextService.TryBuildBookingOfferHandoffPrefill(
            contextualUserInput,
            session.Messages);

        var intentRouting = _intentRoutingService.ResolveIntentAndState(
            contextualUserInput,
            normalizedUserInput,
            bookingOfferHandoffPrefill,
            session.CurrentIntent,
            session.IntentState,
            guardrail);

        var prompt = _promptBuilder.Build(
            contextualUserInput,
            intentRouting.Detection,
            intentRouting.UpdatedIntentState,
            guardrail);

        return new ChatbotTurnPreparationResult
        {
            Guardrail = guardrail,
            ContextualUserInput = contextualUserInput,
            ScopedGreetingReply = scopedGreetingReply,
            IntentRouting = intentRouting,
            Prompt = prompt
        };
    }
}
