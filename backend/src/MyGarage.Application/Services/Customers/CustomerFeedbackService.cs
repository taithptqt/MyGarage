using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CustomerFeedbackService : GenericService<CustomerFeedback>, ICustomerFeedbackService
    {
        private readonly IMapper _mapper;

        public CustomerFeedbackService(ICustomerFeedbackRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public IEnumerable<CustomerFeedbackResponseDto> MapToDtos(IEnumerable<CustomerFeedback> items)
        {
            return _mapper.Map<IEnumerable<CustomerFeedbackResponseDto>>(items);
        }

        public async Task<CustomerFeedbackResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<CustomerFeedbackResponseDto>(item);
        }

        public async Task<CustomerFeedbackResponseDto> CreateDtoAsync(CustomerFeedbackCreateDto itemDto, long? overrideCustomerId = null)
        {
            var entity = _mapper.Map<CustomerFeedback>(itemDto);
            if (overrideCustomerId.HasValue)
            {
                entity.CustomerId = overrideCustomerId.Value;
            }

            var createdItem = await AddAsync(entity);
            return _mapper.Map<CustomerFeedbackResponseDto>(createdItem);
        }

        public async Task<CustomerFeedbackResponseDto?> UpdateDtoAsync(long id, CustomerFeedbackUpdateDto itemDto, bool preserveCustomerId = false)
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
            return updatedItem == null ? null : _mapper.Map<CustomerFeedbackResponseDto>(updatedItem);
        }
    }
}
