// Mục đích: Xử lý nghiệp vụ cho bản ghi gán voucher theo khách hàng.
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CustomerVoucherService : GenericService<CustomerVoucher>, ICustomerVoucherService
    {
        private readonly IMapper _mapper;

        public CustomerVoucherService(ICustomerVoucherRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public IEnumerable<CustomerVoucherResponseDto> MapToDtos(IEnumerable<CustomerVoucher> items)
        {
            return _mapper.Map<IEnumerable<CustomerVoucherResponseDto>>(items);
        }

        public async Task<CustomerVoucherResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<CustomerVoucherResponseDto>(item);
        }

        public async Task<CustomerVoucherResponseDto> CreateDtoAsync(CustomerVoucherCreateDto itemDto)
        {
            var entity = _mapper.Map<CustomerVoucher>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<CustomerVoucherResponseDto>(createdItem);
        }

        public async Task<CustomerVoucherResponseDto?> UpdateDtoAsync(long id, CustomerVoucherUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<CustomerVoucherResponseDto>(updatedItem);
        }
    }
}
