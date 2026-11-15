using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CustomerVehicleService : GenericService<CustomerVehicle>, ICustomerVehicleService
    {
        private readonly IMapper _mapper;

        public CustomerVehicleService(ICustomerVehicleRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public IEnumerable<CustomerVehicleResponseDto> MapToDtos(IEnumerable<CustomerVehicle> items)
        {
            return _mapper.Map<IEnumerable<CustomerVehicleResponseDto>>(items);
        }

        public async Task<CustomerVehicleResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<CustomerVehicleResponseDto>(item);
        }

        public async Task<CustomerVehicleResponseDto> CreateDtoAsync(CustomerVehicleCreateDto itemDto, long? overrideCustomerId = null)
        {
            var entity = _mapper.Map<CustomerVehicle>(itemDto);
            if (overrideCustomerId.HasValue)
            {
                entity.CustomerId = overrideCustomerId.Value;
            }

            var createdItem = await AddAsync(entity);
            return _mapper.Map<CustomerVehicleResponseDto>(createdItem);
        }

        public async Task<CustomerVehicleResponseDto?> UpdateDtoAsync(long id, CustomerVehicleUpdateDto itemDto, bool preserveCustomerId = false)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            var preservedCustomerId = existingItem.CustomerId;
            _mapper.Map(itemDto, existingItem);
            if (preserveCustomerId)
            {
                existingItem.CustomerId = preservedCustomerId;
            }

            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<CustomerVehicleResponseDto>(updatedItem);
        }
    }
}
