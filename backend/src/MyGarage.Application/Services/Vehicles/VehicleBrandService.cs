using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class VehicleBrandService : GenericService<VehicleBrand>, IVehicleBrandService
    {
        private readonly IMapper _mapper;

        public VehicleBrandService(IVehicleBrandRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<VehicleBrandResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<VehicleBrandResponseDto>>(items);
        }

        public List<VehicleBrandResponseDto> MapToDtos(IEnumerable<VehicleBrand> items)
        {
            return _mapper.Map<List<VehicleBrandResponseDto>>(items);
        }

        public async Task<VehicleBrandResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<VehicleBrandResponseDto>(item);
        }

        public async Task<VehicleBrandResponseDto> CreateDtoAsync(VehicleBrandCreateDto itemDto)
        {
            var entity = _mapper.Map<VehicleBrand>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<VehicleBrandResponseDto>(createdItem);
        }

        public async Task<VehicleBrandResponseDto?> UpdateDtoAsync(long id, VehicleBrandUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<VehicleBrandResponseDto>(updatedItem);
        }
    }
}
