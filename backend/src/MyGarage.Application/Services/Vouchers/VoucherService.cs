// Mục đích: Xử lý nghiệp vụ voucher chính thông qua tầng service tổng quát.
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class VoucherService : GenericService<Voucher>, IVoucherService
    {
        private const string VoucherStatusActive = nameof(VoucherStatusCode.ACTIVE);
        private const string VoucherStatusPaused = nameof(VoucherStatusCode.PAUSED);
        private const string VoucherStatusArchived = nameof(VoucherStatusCode.ARCHIVED);
        private const string VoucherStatusExpired = nameof(VoucherStatusCode.EXPIRED);
        private const string VoucherStatusScheduled = nameof(VoucherStatusCode.SCHEDULED);
        private const string VoucherStatusExhausted = nameof(VoucherStatusCode.EXHAUSTED);

        private readonly IMapper _mapper;

        public VoucherService(IVoucherRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<(IReadOnlyList<VoucherResponseDto> Items, int Total)> GetPageDtosByIdAsync(int page, int pageSize)
        {
            var (paged, total) = await GetPageByIdAsync(page, pageSize);
            return (_mapper.Map<List<VoucherResponseDto>>(paged), total);
        }

        public async Task<VoucherResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<VoucherResponseDto>(item);
        }

        public async Task<VoucherResponseDto> CreateDtoAsync(VoucherCreateDto itemDto)
        {
            var entity = _mapper.Map<Voucher>(itemDto);
            entity.VoucherName ??= entity.Code;
            entity.Status ??= ResolveStatus(entity);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<VoucherResponseDto>(createdItem);
        }

        public async Task<VoucherResponseDto?> UpdateDtoAsync(long id, VoucherUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<VoucherResponseDto>(updatedItem);
        }

        public async Task<VoucherResponseDto?> UpdateStatusDtoAsync(long id, string status, bool isActive)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            existingItem.Status = status;
            existingItem.IsActive = isActive;
            existingItem.UpdatedAt = VietnamTime.Now;

            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<VoucherResponseDto>(updatedItem);
        }

        private static string ResolveStatus(Voucher voucher)
        {
            var now = VietnamTime.Now;

            if (voucher.IsDeleted == true)
            {
                return VoucherStatusArchived;
            }

            if (voucher.IsActive == false)
            {
                return VoucherStatusPaused;
            }

            if (voucher.EndDate < now)
            {
                return VoucherStatusExpired;
            }

            if (voucher.StartDate > now)
            {
                return VoucherStatusScheduled;
            }

            if (voucher.UsageLimit.HasValue && voucher.UsageLimit > 0 && voucher.UsedCount.HasValue && voucher.UsedCount >= voucher.UsageLimit)
            {
                return VoucherStatusExhausted;
            }

            return VoucherStatusActive;
        }
    }
}
