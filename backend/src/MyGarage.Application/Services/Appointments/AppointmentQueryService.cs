using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public partial class AppointmentQueryService : IAppointmentQueryService
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IAppointmentStore _store;
        private readonly IAppointmentDetailAssembler _appointmentDetailAssembler;
        private readonly ISystemClock _clock;
        private readonly IMaintenanceReminderService _maintenanceReminderService;
        private readonly IAppLogger<AppointmentQueryService> _logger;

        public AppointmentQueryService(
            IAppointmentRepository appointmentRepository,
            IAppointmentStore store,
            IAppointmentDetailAssembler appointmentDetailAssembler,
            ISystemClock clock,
            IMaintenanceReminderService maintenanceReminderService,
            IAppLogger<AppointmentQueryService> logger)
        {
            _appointmentRepository = appointmentRepository;
            _store = store;
            _appointmentDetailAssembler = appointmentDetailAssembler;
            _clock = clock;
            _maintenanceReminderService = maintenanceReminderService;
            _logger = logger;
        }

        public async Task<(IEnumerable<AppointmentDetailResponseDto> Items, int Total)> GetDetailedPageAsync(
            long? branchId,
            long? customerId,
            string? status,
            int page,
            int pageSize)
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
