using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IConversationRepository : IGenericRepository<Conversation>
    {
        Task<IReadOnlyList<Conversation>> GetInboxAsync(
            long? branchId,
            string? status,
            bool? isUnread,
            long? customerId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<Conversation?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default);

        Task<Conversation?> GetByCustomerAndContextAsync(
            long customerId,
            long branchId,
            long? appointmentId,
            long? serviceOrderId,
            long? vehicleId,
            CancellationToken cancellationToken = default);
    }
}
