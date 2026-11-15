using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceOrderSnapshotService : GenericService<ServiceOrderSnapshot>, IServiceOrderSnapshotService
    {
        private readonly IMapper _mapper;

        public ServiceOrderSnapshotService(IServiceOrderSnapshotRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<ServiceOrderSnapshotResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<ServiceOrderSnapshotResponseDto>>(items);
        }

        public async Task<ServiceOrderSnapshotResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<ServiceOrderSnapshotResponseDto>(item);
        }

        public async Task<ServiceOrderSnapshotResponseDto> CreateDtoAsync(ServiceOrderSnapshotCreateDto itemDto)
        {
            var entity = _mapper.Map<ServiceOrderSnapshot>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<ServiceOrderSnapshotResponseDto>(createdItem);
        }

        public async Task<ServiceOrderSnapshotResponseDto?> UpdateDtoAsync(long id, ServiceOrderSnapshotUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<ServiceOrderSnapshotResponseDto>(updatedItem);
        }
    }
}
