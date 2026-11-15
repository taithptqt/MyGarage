using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class DeliveryService : GenericService<Delivery>, IDeliveryService
    {
        private readonly IMapper _mapper;

        public DeliveryService(IDeliveryRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public IEnumerable<DeliveryResponseDto> MapToDtos(IEnumerable<Delivery> items)
        {
            return _mapper.Map<IEnumerable<DeliveryResponseDto>>(items);
        }

        public async Task<DeliveryResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<DeliveryResponseDto>(item);
        }

        public async Task<DeliveryResponseDto> CreateDtoAsync(DeliveryCreateDto itemDto)
        {
            var entity = _mapper.Map<Delivery>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<DeliveryResponseDto>(createdItem);
        }

        public async Task<DeliveryResponseDto?> UpdateDtoAsync(long id, DeliveryUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<DeliveryResponseDto>(updatedItem);
        }
    }
}
