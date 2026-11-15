using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentReadService
    {
        Task<Appointment?> GetByIdAsync(object id);
        Task<IEnumerable<AppointmentDetailResponseDto>> GetAllDetailedAsync(string? status = null);
        Task<(IEnumerable<AppointmentDetailResponseDto> Items, int Total)> GetDetailedPageAsync(long? branchId, long? customerId, string? status, int page, int pageSize);
        Task<IEnumerable<AppointmentDetailResponseDto>> GetCustomerAppointmentsAsync(long customerId);
        Task<IEnumerable<AppointmentDetailResponseDto>> GetBranchAppointmentsAsync(long branchId, string? status = null);
        Task<AppointmentDetailResponseDto?> GetDetailedAsync(long id);
    }
}
