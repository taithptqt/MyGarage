using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentRepository : IGenericRepository<Appointment>
    {
        Task<IEnumerable<Appointment>> GetByBranchAndDateAsync(long branchId, DateOnly date);
        Task<IEnumerable<Appointment>> GetByCustomerIdAsync(long customerId);
        Task<IEnumerable<Appointment>> GetAllAsync(string? status = null);
        Task<IEnumerable<Appointment>> GetByBranchIdAsync(long branchId, string? status = null);
        Task<Appointment?> GetDetailedByIdAsync(long id);
    }
}
