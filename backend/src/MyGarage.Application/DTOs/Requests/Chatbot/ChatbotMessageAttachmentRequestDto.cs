using System.ComponentModel.DataAnnotations;

namespace MyGarage.DTOs.Requests.Chatbot;

public class ChatbotMessageAttachmentRequestDto
{
    [StringLength(255)]
    public string? FileName { get; set; }

    [StringLength(128)]
    public string? ContentType { get; set; }

    [Range(0, long.MaxValue)]
    public long? FileSize { get; set; }
}
