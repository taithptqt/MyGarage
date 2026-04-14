using System.Collections.Generic;
using MyGarage.DTOs.Requests.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotRequestValidator
{
    IReadOnlyList<string> ValidateCreateSession(CreateChatbotSessionRequestDto request);

    IReadOnlyList<string> ValidateSaveMessage(SaveChatbotMessageRequestDto request);
}
