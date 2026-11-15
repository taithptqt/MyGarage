using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class VehicleService : GenericService<Vehicle>, IVehicleService
    {
        private readonly IMapper _mapper;

        public VehicleService(IVehicleRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<VehicleResponseDto> MapToDtos(IEnumerable<Vehicle> items)
        {
            return _mapper.Map<List<VehicleResponseDto>>(items);
        }

        public async Task<VehicleResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<VehicleResponseDto>(item);
        }

        public async Task<VehicleResponseDto> CreateDtoAsync(VehicleCreateDto itemDto)
        {
            var entity = _mapper.Map<Vehicle>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<VehicleResponseDto>(createdItem);
        }

        public async Task<VehicleResponseDto?> UpdateDtoAsync(long id, VehicleUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<VehicleResponseDto>(updatedItem);
        }
    }
}
