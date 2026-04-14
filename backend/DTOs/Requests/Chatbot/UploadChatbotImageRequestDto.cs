using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MyGarage.DTOs.Requests.Chatbot;

public class UploadChatbotImageRequestDto
{
    public long? SessionId { get; set; }

    [StringLength(128)]
    public string? SessionToken { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    [Required]
    public IFormFile? File { get; set; }
}
