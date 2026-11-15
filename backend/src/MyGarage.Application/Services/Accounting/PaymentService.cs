using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class PaymentService : GenericService<Payment>, IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IMapper _mapper;

        public PaymentService(IPaymentRepository repository, IMapper mapper) : base(repository)
        {
            _paymentRepository = repository;
            _mapper = mapper;
        }

        public List<PaymentResponseDto> MapToDtos(IEnumerable<Payment> items)
            => _mapper.Map<List<PaymentResponseDto>>(items);

        public PaymentResponseDto MapToDto(Payment item)
            => _mapper.Map<PaymentResponseDto>(item);

        public async Task<PaymentResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<PaymentResponseDto>(item);
        }

        public Payment MapToEntity(PaymentCreateDto itemDto)
            => _mapper.Map<Payment>(itemDto);

        public void MapUpdate(PaymentUpdateDto itemDto, Payment existingItem)
            => _mapper.Map(itemDto, existingItem);

        public Task<IEnumerable<Payment>> GetByCustomerPayerAsync(long customerId)
            => _paymentRepository.GetByCustomerPayerAsync(customerId);
    }
}
