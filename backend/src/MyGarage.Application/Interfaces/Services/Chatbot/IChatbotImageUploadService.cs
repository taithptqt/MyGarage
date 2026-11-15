using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotImageUploadService
{
    Task<ChatbotImageUploadResponseDto> UploadAndAnalyzeAsync(
        long customerId,
        UploadChatbotImageInput request,
        CancellationToken cancellationToken = default);
}
