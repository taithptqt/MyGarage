using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Repositories
{
    public class AppointmentRepository : GenericRepository<Appointment>, IAppointmentRepository
    {
        public AppointmentRepository(IGarageContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Appointment>> GetAllDetailedAsync(string? status = null)
        {
            var query = BuildDetailedQuery();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToUpperInvariant();
                if (normalized == nameof(AppointmentStatus.CANCELED) || normalized == nameof(AppointmentStatus.CANCELLED))
                {
                    query = query.Where(a => a.Status.ToUpper() == nameof(AppointmentStatus.CANCELED)
                        || a.Status.ToUpper() == nameof(AppointmentStatus.CANCELLED));
                }
                else
                {
                    query = query.Where(a => a.Status.ToUpper() == normalized);
                }
            }

            return await query
                .OrderByDescending(a => a.StartTime)
                .ThenByDescending(a => a.Id)
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetByBranchAndDateAsync(long branchId, DateOnly date)
        {
            var start = date.ToDateTime(TimeOnly.MinValue);
            var end = start.AddDays(1);

            return await BuildDetailedQuery()
                .Where(a => a.BranchId == branchId
                    && a.StartTime >= start
                    && a.StartTime < end
                    && (a.Status.ToUpper() != nameof(AppointmentStatus.CANCELED)
                        && a.Status.ToUpper() != nameof(AppointmentStatus.CANCELLED)))
                .OrderBy(a => a.StartTime)
                .ThenBy(a => a.Id)
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetByCustomerIdAsync(long customerId)
        {
            return await BuildDetailedQuery()
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.StartTime)
                .ThenByDescending(a => a.Id)
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetByBranchIdAsync(long branchId, string? status = null)
        {
            var query = BuildDetailedQuery()
                .Where(a => a.BranchId == branchId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToUpperInvariant();
                if (normalized == nameof(AppointmentStatus.CANCELED) || normalized == nameof(AppointmentStatus.CANCELLED))
                {
                    query = query.Where(a => a.Status.ToUpper() == nameof(AppointmentStatus.CANCELED)
                        || a.Status.ToUpper() == nameof(AppointmentStatus.CANCELLED));
                }
                else
                {
                    query = query.Where(a => a.Status.ToUpper() == normalized);
                }
            }

            return await query
                .OrderByDescending(a => a.StartTime)
                .ThenByDescending(a => a.Id)
                .ToListAsync();
        }

        public async Task<Appointment?> GetDetailedByIdAsync(long id)
        {
            return await BuildDetailedQuery()
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        private IQueryable<Appointment> BuildDetailedQuery()
        {
            return _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Vehicle)
                .Include(a => a.Branch)
                .Include(a => a.BookingGroup)
                .Include(a => a.ServiceOrders);
        }
    }
}
