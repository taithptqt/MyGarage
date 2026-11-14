using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        public async Task<(IEnumerable<AppointmentDetailResponseDto> Items, int Total)> GetDetailedPageAsync(long? branchId, long? customerId, string? status, int page, int pageSize)
        {
            var (items, total) = await _appointmentRepository.GetDetailedPageAsync(branchId, customerId, status, page, pageSize);
            return (await _appointmentDetailAssembler.AssembleManyAsync(items), total);
        }
        public async Task<IEnumerable<AppointmentDetailResponseDto>> GetCustomerAppointmentsAsync(long customerId)
        {
            var appointments = await _appointmentRepository.GetByCustomerIdAsync(customerId);
            return await _appointmentDetailAssembler.AssembleManyAsync(appointments);
        }

        public async Task<IEnumerable<AppointmentDetailResponseDto>> GetAllDetailedAsync(string? status = null)
        {
            var appointments = await _appointmentRepository.GetAllDetailedAsync(status);
            return await _appointmentDetailAssembler.AssembleManyAsync(appointments);
        }

        public async Task<IEnumerable<AppointmentDetailResponseDto>> GetBranchAppointmentsAsync(long branchId, string? status = null)
        {
            var appointments = await _appointmentRepository.GetByBranchIdAsync(branchId, status);
            return await _appointmentDetailAssembler.AssembleManyAsync(appointments);
        }

        public async Task<AppointmentDetailResponseDto?> GetDetailedAsync(long id)
        {
            var appointment = await _appointmentRepository.GetDetailedByIdAsync(id);
            if (appointment == null) return null;

            return await _appointmentDetailAssembler.AssembleAsync(appointment);
        }

    }
}
