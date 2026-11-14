using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;

namespace MyGarage.Interfaces;

public interface IChatbotOrchestrator
{
    Task<OrchestratedChatbotReplyDto> OrchestrateAsync(
        long customerId,
        OrchestrateChatbotRequestDto request,
        CancellationToken cancellationToken = default);
}
