using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public interface IChatbotRateLimitHook
{
    Task EnsureCanCreateSessionAsync(long customerId, CancellationToken cancellationToken = default);

    Task EnsureCanSendMessageAsync(long customerId, long sessionId, CancellationToken cancellationToken = default);
}
