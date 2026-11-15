using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class VehicleInsuranceService : GenericService<VehicleInsurance>, IVehicleInsuranceService
    {
        private readonly IMapper _mapper;

        public VehicleInsuranceService(IVehicleInsuranceRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<VehicleInsuranceResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<VehicleInsuranceResponseDto>>(items);
        }

        public async Task<VehicleInsuranceResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<VehicleInsuranceResponseDto>(item);
        }

        public async Task<VehicleInsuranceResponseDto> CreateDtoAsync(VehicleInsuranceCreateDto itemDto)
        {
            var entity = _mapper.Map<VehicleInsurance>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<VehicleInsuranceResponseDto>(createdItem);
        }

        public async Task<VehicleInsuranceResponseDto?> UpdateDtoAsync(long id, VehicleInsuranceUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<VehicleInsuranceResponseDto>(updatedItem);
        }
    }
}
