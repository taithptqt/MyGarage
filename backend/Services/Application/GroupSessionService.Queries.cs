using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class GroupSessionService
    {
        private async Task<GroupSessionResponseDto?> GetGroupDetailCoreAsync(GroupSession? group)
        {
            if (group == null)
            {
                return null;
            }

            var appointments = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Branch)
                .Include(a => a.Customer)
                .Include(a => a.GroupSession)
                .Include(a => a.Vehicle)
                    .ThenInclude(v => v!.Model)
                        .ThenInclude(m => m.Brand)
                .Where(a => a.GroupSessionId == group.Id)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            var serviceNameMap = await BuildServiceNameMapAsync(appointments);
            var packageNameMap = await BuildPackageNameMapAsync(appointments);
            var dto = MapHeaderOnly(group);
            dto.Appointments = appointments.Select(a => MapAppointment(a, serviceNameMap, packageNameMap)).ToList();
            return dto;
        }

        public async Task<IEnumerable<GroupSessionResponseDto>> GetByCustomerAsync(long customerId)
        {
            var groups = await _context.GroupSessions
                .AsNoTracking()
                .Where(x => x.CustomerId == customerId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return groups.Select(MapHeaderOnly);
        }

        public async Task<GroupSessionResponseDto?> GetByCustomerAndIdAsync(long customerId, long GroupSessionId)
        {
            var group = await _context.GroupSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == GroupSessionId && x.CustomerId == customerId);
            return await GetGroupDetailCoreAsync(group);
        }

        public async Task<IEnumerable<GroupSessionResponseDto>> GetByBranchAsync(long branchId)
        {
            var groups = await _context.GroupSessions
                .AsNoTracking()
                .Where(x => x.BranchId == branchId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return groups.Select(MapHeaderOnly);
        }

        public async Task<GroupSessionResponseDto?> GetByBranchAndIdAsync(long branchId, long GroupSessionId)
        {
            var group = await _context.GroupSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == GroupSessionId && x.BranchId == branchId);
            return await GetGroupDetailCoreAsync(group);
        }
    }
}
