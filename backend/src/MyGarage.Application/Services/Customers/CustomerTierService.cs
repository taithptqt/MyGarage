using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CustomerTierService : GenericService<CustomerTier>, ICustomerTierService
    {
        private readonly IMapper _mapper;

        public CustomerTierService(ICustomerTierRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<CustomerTierResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<CustomerTierResponseDto>>(items);
        }

        public async Task<CustomerTierResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<CustomerTierResponseDto>(item);
        }

        public async Task<CustomerTierResponseDto> CreateDtoAsync(CustomerTierCreateDto itemDto)
        {
            var entity = _mapper.Map<CustomerTier>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<CustomerTierResponseDto>(createdItem);
        }

        public async Task<CustomerTierResponseDto?> UpdateDtoAsync(long id, CustomerTierUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<CustomerTierResponseDto>(updatedItem);
        }
    }
}
