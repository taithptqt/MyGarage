using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Ai;

public interface IAiChatbotSessionStore
{
    Task<bool> ChatbotSessionExistsAsync(long sessionId, long customerId, CancellationToken ct = default);
}
