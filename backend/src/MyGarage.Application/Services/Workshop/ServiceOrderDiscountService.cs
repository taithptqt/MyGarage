using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceOrderDiscountService : GenericService<ServiceOrderDiscount>, IServiceOrderDiscountService
    {
        private readonly IMapper _mapper;

        public ServiceOrderDiscountService(IServiceOrderDiscountRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<ServiceOrderDiscountResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<ServiceOrderDiscountResponseDto>>(items);
        }

        public async Task<ServiceOrderDiscountResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<ServiceOrderDiscountResponseDto>(item);
        }

        public async Task<ServiceOrderDiscountResponseDto> CreateDtoAsync(ServiceOrderDiscountCreateDto itemDto)
        {
            var entity = _mapper.Map<ServiceOrderDiscount>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<ServiceOrderDiscountResponseDto>(createdItem);
        }

        public async Task<ServiceOrderDiscountResponseDto?> UpdateDtoAsync(long id, ServiceOrderDiscount existingItem, ServiceOrderDiscountUpdateDto itemDto)
        {
            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<ServiceOrderDiscountResponseDto>(updatedItem);
        }
    }
}
