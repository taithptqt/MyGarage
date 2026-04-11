using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Chatbot;

public class ChatbotToolCallRequestDto
{
    [Required]
    [StringLength(100)]
    public string ToolName { get; set; } = null!;

    public string? ToolArgs { get; set; }
}
