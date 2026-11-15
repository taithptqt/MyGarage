using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class StandardServiceService : GenericService<StandardService>, IStandardServiceService
    {
        private readonly IMapper _mapper;

        public StandardServiceService(IStandardServiceRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<StandardServiceResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<StandardServiceResponseDto>>(items);
        }

        public async Task<StandardServiceResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<StandardServiceResponseDto>(item);
        }

        public async Task<StandardServiceResponseDto> CreateDtoAsync(StandardServiceCreateDto itemDto)
        {
            var entity = _mapper.Map<StandardService>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<StandardServiceResponseDto>(createdItem);
        }

        public async Task<StandardServiceResponseDto?> UpdateDtoAsync(long id, StandardServiceUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<StandardServiceResponseDto>(updatedItem);
        }
    }
}
