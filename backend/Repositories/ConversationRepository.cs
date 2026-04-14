using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Repositories
{
    public class ConversationRepository : GenericRepository<Conversation>, IConversationRepository
    {
        public ConversationRepository(IGarageContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<Conversation>> GetInboxAsync(
            long? branchId,
            string? status,
            bool? isUnread,
            long? customerId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var safePage = page <= 0 ? 1 : page;
            var safePageSize = pageSize <= 0 ? 20 : pageSize;

            var query = _dbSet
                .AsNoTracking()
                .AsQueryable();

            if (branchId.HasValue)
            {
                query = query.Where(x => x.BranchId == branchId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x => x.Status == status);
            }

            if (customerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == customerId.Value);
            }

            // Placeholder unread filter for now. Will be replaced by read-state tables in service phase.
            if (isUnread == true)
            {
                query = query.Where(x => x.ChatMessages.Any());
            }

            return await query
                .OrderByDescending(x => x.LastMessageAt ?? x.UpdatedAt ?? x.CreatedAt)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .ToListAsync(cancellationToken);
        }

        public Task<Conversation?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default)
        {
            return _dbSet
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Staff)
                .Include(x => x.AssignedStaff)
                .Include(x => x.Branch)
                .Include(x => x.Appointment)
                .Include(x => x.ServiceOrder)
                .Include(x => x.Vehicle)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public Task<Conversation?> GetByCustomerAndContextAsync(
            long customerId,
            long branchId,
            long? appointmentId,
            long? serviceOrderId,
            long? vehicleId,
            CancellationToken cancellationToken = default)
        {
            return _dbSet
                .AsNoTracking()
                .Where(x => x.CustomerId == customerId && x.BranchId == branchId)
                .Where(x => x.AppointmentId == appointmentId)
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Where(x => x.VehicleId == vehicleId)
                .OrderByDescending(x => x.LastMessageAt ?? x.UpdatedAt ?? x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
