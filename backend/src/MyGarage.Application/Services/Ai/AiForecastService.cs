using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AiForecastService : GenericService<AiForecast>, IAiForecastService
    {
        private readonly IMapper _mapper;

        public AiForecastService(IAiForecastRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<AiForecastResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<AiForecastResponseDto>(item);
        }

        public async Task<AiForecastResponseDto> CreateDtoAsync(AiForecastCreateDto itemDto)
        {
            var entity = _mapper.Map<AiForecast>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<AiForecastResponseDto>(createdItem);
        }

        public async Task<AiForecastResponseDto?> UpdateDtoAsync(long id, AiForecastUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<AiForecastResponseDto>(updatedItem);
        }
    }
}
