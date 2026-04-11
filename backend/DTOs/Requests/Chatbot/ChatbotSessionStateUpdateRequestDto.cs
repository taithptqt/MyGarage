using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Chatbot;

public class ChatbotSessionStateUpdateRequestDto
{
    [StringLength(20)]
    public string? Status { get; set; }

    [StringLength(100)]
    public string? CurrentIntent { get; set; }

    public string? IntentState { get; set; }
}
