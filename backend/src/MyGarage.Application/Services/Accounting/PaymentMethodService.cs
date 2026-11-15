using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class PaymentMethodService : GenericService<PaymentMethod>, IPaymentMethodService
    {
        private readonly IMapper _mapper;

        public PaymentMethodService(IPaymentMethodRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<PaymentMethodResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<PaymentMethodResponseDto>>(items);
        }

        public async Task<PaymentMethodResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<PaymentMethodResponseDto>(item);
        }

        public async Task<PaymentMethodResponseDto> CreateDtoAsync(PaymentMethodCreateDto itemDto)
        {
            var entity = _mapper.Map<PaymentMethod>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<PaymentMethodResponseDto>(createdItem);
        }

        public async Task<PaymentMethodResponseDto?> UpdateDtoAsync(long id, PaymentMethodUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<PaymentMethodResponseDto>(updatedItem);
        }
    }
}
