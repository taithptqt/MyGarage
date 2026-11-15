using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Invoices;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class InvoiceDetailService : GenericService<InvoiceDetail>, IInvoiceDetailService
    {
        private readonly IInvoiceDetailRepository _detailRepository;
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly IInvoiceApplicationService _invoiceAppService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IMapper _mapper;

        public InvoiceDetailService(
            IInvoiceDetailRepository repository,
            IMapper mapper,
            IInvoiceRepository invoiceRepository,
            IInvoiceApplicationService invoiceAppService,
            IServiceOrderService serviceOrderService) : base(repository)
        {
            _detailRepository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _invoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
            _invoiceAppService = invoiceAppService ?? throw new ArgumentNullException(nameof(invoiceAppService));
            _serviceOrderService = serviceOrderService ?? throw new ArgumentNullException(nameof(serviceOrderService));
        }

        public async Task<IEnumerable<InvoiceDetailResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<InvoiceDetailResponseDto>>(items);
        }

        public IEnumerable<InvoiceDetailResponseDto> MapToDtos(IEnumerable<InvoiceDetail> items) =>
            _mapper.Map<IEnumerable<InvoiceDetailResponseDto>>(items);

        public InvoiceDetailResponseDto MapToDto(InvoiceDetail item) => _mapper.Map<InvoiceDetailResponseDto>(item);

        public async Task<InvoiceDetailResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<InvoiceDetailResponseDto>(item);
        }

        public async Task<InvoiceDetailResponseDto> CreateDtoAsync(InvoiceDetailCreateDto itemDto)
        {
            var entity = _mapper.Map<InvoiceDetail>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<InvoiceDetailResponseDto>(createdItem);
        }

        public async Task<InvoiceDetailResponseDto?> UpdateDtoAsync(long id, InvoiceDetailUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            var originalInvoiceId = existingItem.InvoiceId;
            _mapper.Map(itemDto, existingItem);
            if (itemDto.InvoiceId <= 0)
                existingItem.InvoiceId = originalInvoiceId;
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<InvoiceDetailResponseDto>(updatedItem);
        }

        public async Task<IEnumerable<InvoiceDetailResponseDto>> GetScopedDtosAsync(long? branchId, CancellationToken ct = default)
        {
            var items = await _detailRepository.GetScopedAsync(branchId, ct);
            return MapToDtos(items);
        }

        public async Task<InvoiceDetailResponseDto?> GetScopedDtoByIdAsync(long id, long? branchId, CancellationToken ct = default)
        {
            var item = await _detailRepository.GetScopedAsync(id, branchId, ct);
            return item == null ? null : MapToDto(item);
        }

        public async Task<InvoiceDetailCommandResult<InvoiceDetailResponseDto>> CreateScopedAsync(
            long? branchId,
            InvoiceDetailCreateDto? itemDto,
            CancellationToken ct = default)
        {
            if (itemDto == null || itemDto.InvoiceId <= 0)
            {
                return InvoiceDetailCommandResult<InvoiceDetailResponseDto>.BadRequest("InvoiceId is required.");
            }

            if (await _invoiceRepository.GetScopedAsync(itemDto.InvoiceId, branchId, ct) == null)
            {
                return InvoiceDetailCommandResult<InvoiceDetailResponseDto>.NotFound();
            }

            var lockError = await ValidateInvoiceDetailMutationAsync(itemDto.InvoiceId, null, ct);
            if (!string.IsNullOrWhiteSpace(lockError))
            {
                return InvoiceDetailCommandResult<InvoiceDetailResponseDto>.BadRequest(lockError);
            }

            try
            {
                var responseDto = await CreateDtoAsync(itemDto);
                return InvoiceDetailCommandResult<InvoiceDetailResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return InvoiceDetailCommandResult<InvoiceDetailResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<InvoiceDetailCommandResult<InvoiceDetailResponseDto>> UpdateScopedAsync(
            long id,
            long? branchId,
            InvoiceDetailUpdateDto? itemDto,
            CancellationToken ct = default)
        {
            var existingItem = await _detailRepository.GetScopedAsync(id, branchId, ct);
            if (existingItem == null)
            {
                return InvoiceDetailCommandResult<InvoiceDetailResponseDto>.NotFound();
            }

            var invoiceId = itemDto?.InvoiceId > 0 ? itemDto.InvoiceId : existingItem.InvoiceId;
            if (await _invoiceRepository.GetScopedAsync(invoiceId, branchId, ct) == null)
            {
                return InvoiceDetailCommandResult<InvoiceDetailResponseDto>.NotFound();
            }

            var lockError = await ValidateInvoiceDetailMutationAsync(invoiceId, existingItem.InvoiceId, ct);
            if (!string.IsNullOrWhiteSpace(lockError))
            {
                return InvoiceDetailCommandResult<InvoiceDetailResponseDto>.BadRequest(lockError);
            }

            if (itemDto == null)
            {
                return InvoiceDetailCommandResult<InvoiceDetailResponseDto>.BadRequest("Invalid invoice detail payload.");
            }

            try
            {
                var responseDto = await UpdateDtoAsync(id, itemDto);
                return responseDto == null
                    ? InvoiceDetailCommandResult<InvoiceDetailResponseDto>.NotFound()
                    : InvoiceDetailCommandResult<InvoiceDetailResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return InvoiceDetailCommandResult<InvoiceDetailResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<InvoiceDetailCommandResult<bool>> DeleteScopedAsync(
            long id,
            long? branchId,
            CancellationToken ct = default)
        {
            var existingItem = await _detailRepository.GetScopedAsync(id, branchId, ct);
            if (existingItem == null)
            {
                return InvoiceDetailCommandResult<bool>.NotFound();
            }

            var lockError = await ValidateInvoiceDetailMutationAsync(existingItem.InvoiceId, existingItem.InvoiceId, ct);
            if (!string.IsNullOrWhiteSpace(lockError))
            {
                return InvoiceDetailCommandResult<bool>.BadRequest(lockError);
            }

            try
            {
                var result = await DeleteAsync(id);
                return result
                    ? InvoiceDetailCommandResult<bool>.Ok(true)
                    : InvoiceDetailCommandResult<bool>.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return InvoiceDetailCommandResult<bool>.BadRequest(ex.Message);
            }
        }

        public async Task<string?> ValidateInvoiceDetailMutationAsync(
            long? invoiceId,
            long? fallbackInvoiceId = null,
            CancellationToken ct = default)
        {
            var targetInvoiceId = invoiceId.GetValueOrDefault();
            if (targetInvoiceId <= 0 && fallbackInvoiceId.HasValue)
            {
                targetInvoiceId = fallbackInvoiceId.Value;
            }

            if (targetInvoiceId <= 0)
            {
                return null;
            }

            var serviceOrderId = await _invoiceAppService.GetServiceOrderIdByInvoiceIdAsync(targetInvoiceId, ct);
            if (!serviceOrderId.HasValue || serviceOrderId.Value <= 0)
            {
                return null;
            }

            var capability = await _serviceOrderService.GetEditCapabilityAsync(serviceOrderId.Value);
            if (capability == null)
            {
                return "Không tìm thấy service order để xử lý chi tiết hóa đơn.";
            }

            if (capability.IsClosedLocked)
            {
                return "Không thể chỉnh sửa chi tiết hóa đơn vì service order đã đóng.";
            }

            if (capability.IsBillingLocked)
            {
                return "Không thể chỉnh sửa chi tiết hóa đơn vì lệnh đã chốt tài chính.";
            }

            return null;
        }
    }
}
