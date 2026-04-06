using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class AppointmentRepository : GenericRepository<Appointment>, IAppointmentRepository
    {
        public AppointmentRepository(IGarageContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Appointment>> GetAllDetailedAsync(string? status = null)
        {
            var query = _dbSet
                .Include(a => a.Branch)
                .Include(a => a.Customer)
                .Include(a => a.Vehicle)
                    .ThenInclude(v => v!.Model)
                        .ThenInclude(m => m.Brand)
                .Include(a => a.GroupSession)
                .AsQueryable();

            query = ApplyStatusFilter(query, status);

            return await query
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetByBranchAndDateAsync(long branchId, DateOnly date)
        {
            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            return await _dbSet
                .Where(a => a.BranchId == branchId
                         && a.StartTime >= dayStart
                         && a.StartTime < dayEnd
                         && (a.Status == null
                             || (a.Status.ToUpper() != nameof(AppointmentStatus.CANCELED)
                                 && a.Status.ToUpper() != nameof(AppointmentStatus.CANCELLED))))
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetByCustomerIdAsync(long customerId)
        {
            return await _dbSet
                .Include(a => a.Branch)
                .Include(a => a.GroupSession)
                .Include(a => a.Vehicle)
                    .ThenInclude(v => v!.Model)
                        .ThenInclude(m => m.Brand)
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Appointment>> GetByBranchIdAsync(long branchId, string? status = null)
        {
            var query = _dbSet
                .Include(a => a.Customer)
                .Include(a => a.GroupSession)
                .Include(a => a.Vehicle)
                    .ThenInclude(v => v!.Model)
                        .ThenInclude(m => m.Brand)
                .Where(a => a.BranchId == branchId);

            query = ApplyStatusFilter(query, status);

            return await query
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();
        }

        private static IQueryable<Appointment> ApplyStatusFilter(IQueryable<Appointment> query, string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return query;
            }

            var normalized = status.Trim().ToUpperInvariant();
            if (normalized == nameof(AppointmentStatus.CANCELED) || normalized == nameof(AppointmentStatus.CANCELLED))
            {
                return query.Where(a =>
                    a.Status != null &&
                    (a.Status.ToUpper() == nameof(AppointmentStatus.CANCELED) ||
                     a.Status.ToUpper() == nameof(AppointmentStatus.CANCELLED)));
            }

            return query.Where(a => a.Status != null && a.Status.ToUpper() == normalized);
        }

        public async Task<Appointment?> GetDetailedByIdAsync(long id)
        {
            return await _dbSet
                .Include(a => a.Branch)
                .Include(a => a.Customer)
                .Include(a => a.GroupSession)
                .Include(a => a.Vehicle)
                    .ThenInclude(v => v!.Model)
                        .ThenInclude(m => m.Brand)
                .FirstOrDefaultAsync(a => a.Id == id);
        }
    }
}

