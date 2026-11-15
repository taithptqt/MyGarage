using System.Collections.Generic;
using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotTurnContextService
{
    string NormalizeForRouting(string? input);

    string NormalizeForSignalMatching(string? input);

    string BuildContextualUserInput(
        string userInput,
        IReadOnlyList<ChatbotMessageResponseDto> history);

    string? TryBuildBookingOfferHandoffPrefill(
        string userInput,
        IReadOnlyList<ChatbotMessageResponseDto> history);

    string? BuildGarageGreetingReply(string normalizedInput);
}
