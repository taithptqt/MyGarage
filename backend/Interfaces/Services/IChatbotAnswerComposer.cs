using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public interface IChatbotAnswerComposer
{
    Task<string> ComposeAsync(
        ChatbotRetrievalResult retrieval,
        long? branchId,
        CancellationToken cancellationToken = default);
}
