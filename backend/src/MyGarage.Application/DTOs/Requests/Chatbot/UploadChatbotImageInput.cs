using System.IO;

namespace MyGarage.DTOs.Requests.Chatbot;

public sealed record UploadChatbotImageInput(
    long? SessionId,
    string? SessionToken,
    string? Note,
    Stream Content,
    string FileName,
    string ContentType,
    long Length);
