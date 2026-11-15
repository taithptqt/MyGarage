using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentRepository : IGenericRepository<Appointment>
    {
        Task<IEnumerable<Appointment>> GetAllDetailedAsync(string? status = null);
        Task<(IReadOnlyList<Appointment> Items, int Total)> GetDetailedPageAsync(long? branchId, long? customerId, string? status, int page, int pageSize);
        Task<IEnumerable<Appointment>> GetByBranchAndDateAsync(long branchId, DateOnly date);
        Task<IEnumerable<Appointment>> GetByCustomerIdAsync(long customerId);
        Task<IEnumerable<Appointment>> GetByBranchIdAsync(long branchId, string? status = null);
        Task<Appointment?> GetDetailedByIdAsync(long id);
    }
}
