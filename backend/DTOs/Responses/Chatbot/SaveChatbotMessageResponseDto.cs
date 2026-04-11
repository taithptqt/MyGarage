using System.Collections.Generic;

namespace MyGarage.DTOs.Responses.Chatbot;

public class SaveChatbotMessageResponseDto
{
    public ChatbotSessionResponseDto Session { get; set; } = null!;

    public IReadOnlyList<ChatbotMessageResponseDto> PersistedMessages { get; set; } = new List<ChatbotMessageResponseDto>();
}
