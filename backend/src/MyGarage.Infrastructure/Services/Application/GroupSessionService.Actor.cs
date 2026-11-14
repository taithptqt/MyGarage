using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class GroupSessionService
    {
        public async Task<long?> ResolveStaffActorIdAsync(long? actorUserId)
        {
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return null;
            }

            var staffExists = await _context.Staffs
                .AsNoTracking()
                .AnyAsync(s => s.Id == actorUserId.Value);

            return staffExists ? actorUserId.Value : null;
        }
    }
}
