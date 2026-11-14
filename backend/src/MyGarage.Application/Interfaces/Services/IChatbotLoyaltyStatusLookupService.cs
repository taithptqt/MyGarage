using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotLoyaltyStatusLookupService
{
    Task<ChatbotCustomerSelfServiceLookupResponseDto?> TryLookupAsync(
        long customerId,
        string userMessage,
        CancellationToken cancellationToken = default);
}
