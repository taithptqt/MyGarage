using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotBookingWorkflowService
{
    Task<ChatbotBookingWorkflowResponseDto> RunAsync(
        long customerId,
        long sessionId,
        long? sessionBranchId,
        string intentStateJson,
        string userMessage,
        CancellationToken cancellationToken = default);
}
