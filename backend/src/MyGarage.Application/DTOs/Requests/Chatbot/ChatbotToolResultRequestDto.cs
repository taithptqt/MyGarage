using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Chatbot;

public class ChatbotToolResultRequestDto
{
    [Required]
    [StringLength(100)]
    public string ToolName { get; set; } = null!;

    public string? ToolResult { get; set; }
}
