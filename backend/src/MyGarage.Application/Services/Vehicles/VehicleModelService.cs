using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class VehicleModelService : GenericService<VehicleModel>, IVehicleModelService
    {
        private readonly IMapper _mapper;

        public VehicleModelService(IVehicleModelRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<VehicleModelResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<VehicleModelResponseDto>>(items);
        }

        public List<VehicleModelResponseDto> MapToDtos(IEnumerable<VehicleModel> items)
        {
            return _mapper.Map<List<VehicleModelResponseDto>>(items);
        }

        public async Task<VehicleModelResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<VehicleModelResponseDto>(item);
        }

        public async Task<VehicleModelResponseDto> CreateDtoAsync(VehicleModelCreateDto itemDto)
        {
            var entity = _mapper.Map<VehicleModel>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<VehicleModelResponseDto>(createdItem);
        }

        public async Task<VehicleModelResponseDto?> UpdateDtoAsync(long id, VehicleModelUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<VehicleModelResponseDto>(updatedItem);
        }
    }
}
