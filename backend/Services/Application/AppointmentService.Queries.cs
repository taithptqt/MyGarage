using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
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
