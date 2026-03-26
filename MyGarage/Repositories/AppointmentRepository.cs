using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class AppointmentRepository : GenericRepository<Appointment>, IAppointmentRepository
    {
        public AppointmentRepository(GarageContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Appointment>> GetByBranchAndDateAsync(long branchId, DateOnly date)
        {
            return await _dbSet
                .Where(a => a.BranchId == branchId
                         && a.ScheduledDate == date
                         && a.Status != "CANCELED")
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetByCustomerIdAsync(long customerId)
        {
            return await _dbSet
                .Include(a => a.Branch)
                .Include(a => a.Vehicle)
                    .ThenInclude(v => v!.Model)
                        .ThenInclude(m => m.Brand)
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.ScheduledDate)
                    .ThenByDescending(a => a.StartTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetByBranchIdAsync(long branchId, string? status = null)
        {
            var query = _dbSet
                .Include(a => a.Customer)
                .Include(a => a.Vehicle)
                    .ThenInclude(v => v!.Model)
                        .ThenInclude(m => m.Brand)
                .Where(a => a.BranchId == branchId);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(a => a.Status == status);

            return await query
                .OrderByDescending(a => a.ScheduledDate)
                    .ThenByDescending(a => a.StartTime)
                .ToListAsync();
        }

        public async Task<Appointment?> GetDetailedByIdAsync(long id)
        {
            return await _dbSet
                .Include(a => a.Branch)
                .Include(a => a.Customer)
                .Include(a => a.Vehicle)
                    .ThenInclude(v => v!.Model)
                        .ThenInclude(m => m.Brand)
                .FirstOrDefaultAsync(a => a.Id == id);
        }
    }
}
