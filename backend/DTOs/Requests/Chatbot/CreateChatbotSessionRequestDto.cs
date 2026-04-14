using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Chatbot;

public class CreateChatbotSessionRequestDto
{
    [StringLength(100)]
    public string? CurrentIntent { get; set; }

    public string? IntentState { get; set; }
}
