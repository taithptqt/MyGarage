using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VehicleConditionLogService : GenericService<VehicleConditionLog>, IVehicleConditionLogService
    {
        private readonly IVehicleConditionLogRepository _vehicleConditionLogRepository;
        private readonly IVehicleConditionLogStore _store;
        private readonly IMapper _mapper;

        public VehicleConditionLogService(
            IVehicleConditionLogRepository repository,
            IVehicleConditionLogStore store,
            IMapper mapper)
            : base(repository)
        {
            _vehicleConditionLogRepository = repository;
            _store = store;
            _mapper = mapper;
        }

        public async Task<IEnumerable<VehicleConditionLogResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<VehicleConditionLogResponseDto>>(items);
        }

        public async Task<VehicleConditionLogResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<VehicleConditionLogResponseDto>(item);
        }

        public async Task<IEnumerable<VehicleConditionLog>> GetByServiceOrderIdAsync(long serviceOrderId)
        {
            return await _vehicleConditionLogRepository.GetByServiceOrderIdAsync(serviceOrderId);
        }

        public async Task<bool> ServiceOrderExistsAsync(long serviceOrderId)
        {
            return await _store.ServiceOrderExistsAsync(serviceOrderId);
        }

        public async Task<bool> TechnicianExistsAsync(long technicianId)
        {
            return await _store.TechnicianExistsAsync(technicianId);
        }
    }
}
