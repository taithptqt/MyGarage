using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;

namespace MyGarage.Services
{
    public partial class BookingGroupService
    {
        public async Task<IEnumerable<BookingGroupResponseDto>> GetByCustomerAsync(long customerId)
        {
            var groups = await _context.BookingGroups
                .AsNoTracking()
                .Where(x => x.CustomerId == customerId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return groups.Select(MapHeaderOnly);
        }

        public async Task<BookingGroupResponseDto?> GetByCustomerAndIdAsync(long customerId, long bookingGroupId)
        {
            var group = await _context.BookingGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == bookingGroupId && x.CustomerId == customerId);
            if (group == null)
            {
                return null;
            }

            var appointments = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Branch)
                .Include(a => a.Customer)
                .Include(a => a.Vehicle)
                    .ThenInclude(v => v!.Model)
                        .ThenInclude(m => m.Brand)
                .Where(a => a.BookingGroupId == bookingGroupId)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            var serviceNameMap = await BuildServiceNameMapAsync(appointments);
            var dto = MapHeaderOnly(group);
            dto.Appointments = appointments.Select(a => MapAppointment(a, serviceNameMap)).ToList();
            return dto;
        }
    }
}
