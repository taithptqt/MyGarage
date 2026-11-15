using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotService
{
    Task<ChatbotSessionResponseDto> CreateSessionAsync(
        long customerId,
        long? branchId,
        CreateChatbotSessionRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ChatbotSessionResponseDto?> GetSessionByIdAsync(
        long customerId,
        long sessionId,
        CancellationToken cancellationToken = default);

    Task<ChatbotSessionResponseDto?> GetSessionByTokenAsync(
        long customerId,
        string sessionToken,
        CancellationToken cancellationToken = default);

    Task<SaveChatbotMessageResponseDto> SaveMessagesAsync(
        long customerId,
        SaveChatbotMessageRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ChatbotSessionResponseDto> UpdateSessionStateAsync(
        long customerId,
        long sessionId,
        ChatbotSessionStateUpdateRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ChatbotSessionResponseDto> MarkSessionHandedOffAsync(
        long customerId,
        long sessionId,
        long conversationId,
        string handoffSummary,
        string? intentState = null,
        CancellationToken cancellationToken = default);
}
