using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ServiceOrderDetailCommandService : IServiceOrderDetailCommandService
    {
        private readonly IServiceOrderDetailService _serviceOrderDetailService;
        private readonly IServiceOrderService _serviceOrderService;

        public ServiceOrderDetailCommandService(
            IServiceOrderDetailService serviceOrderDetailService,
            IServiceOrderService serviceOrderService)
        {
            _serviceOrderDetailService = serviceOrderDetailService ?? throw new ArgumentNullException(nameof(serviceOrderDetailService));
            _serviceOrderService = serviceOrderService ?? throw new ArgumentNullException(nameof(serviceOrderService));
        }

        public async Task<IReadOnlyList<ServiceOrderDetailResponseDto>> GetAllAsync(
            long? serviceOrderId = null,
            CancellationToken ct = default)
        {
            IEnumerable<ServiceOrderDetail> items;
            if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                items = await _serviceOrderDetailService.GetByServiceOrderIdAsync(serviceOrderId.Value, ct);
            }
            else
            {
                items = await _serviceOrderDetailService.GetAllAsync();
            }

            return _serviceOrderDetailService.MapToDtos(items).ToList();
        }

        public Task<ServiceOrderDetailResponseDto?> GetByIdAsync(long id, CancellationToken ct = default)
        {
            return _serviceOrderDetailService.GetDtoByIdAsync(id);
        }

        public async Task<ServiceOrderDetailCommandResult<ServiceOrderDetailResponseDto>> CreateAsync(
            ServiceOrderDetailCreateDto? itemDto,
            CancellationToken ct = default)
        {
            try
            {
                if (itemDto == null || itemDto.ServiceOrderId <= 0)
                {
                    return ServiceOrderDetailCommandResult<ServiceOrderDetailResponseDto>.BadRequest(
                        "Thiếu ServiceOrderId hợp lệ để tạo hạng mục service order.");
                }

                await _serviceOrderService.EnsureWorkshopEditableAsync(itemDto.ServiceOrderId);

                var entity = _serviceOrderDetailService.MapToEntity(itemDto);
                ServiceOrderDetailDomainRules.EnsureNoDirectSupplementaryCreate(entity);
                var createdItem = await _serviceOrderDetailService.AddAsync(entity);
                var responseDto = _serviceOrderDetailService.MapToDto(createdItem);
                return ServiceOrderDetailCommandResult<ServiceOrderDetailResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return ServiceOrderDetailCommandResult<ServiceOrderDetailResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<ServiceOrderDetailCommandResult<ServiceOrderDetailResponseDto>> UpdateAsync(
            long id,
            ServiceOrderDetailUpdateDto? itemDto,
            CancellationToken ct = default)
        {
            try
            {
                var existingItem = await _serviceOrderDetailService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return ServiceOrderDetailCommandResult<ServiceOrderDetailResponseDto>.NotFound();
                }

                var targetServiceOrderId = itemDto?.ServiceOrderId > 0
                    ? itemDto.ServiceOrderId
                    : existingItem.ServiceOrderId;
                await _serviceOrderService.EnsureWorkshopEditableAsync(targetServiceOrderId);
                ServiceOrderDetailDomainRules.EnsureNoMutationForAppliedApprovedDetail(existingItem);

                if (itemDto == null)
                {
                    return ServiceOrderDetailCommandResult<ServiceOrderDetailResponseDto>.BadRequest(
                        "Dữ liệu cập nhật hạng mục service order không hợp lệ.");
                }

                var sourceSnapshot = ServiceOrderDetailDomainRules.CaptureSourceSnapshot(existingItem);
                _serviceOrderDetailService.ApplyUpdate(itemDto, existingItem);
                if (itemDto.ServiceOrderId <= 0)
                {
                    existingItem.ServiceOrderId = targetServiceOrderId;
                }

                ServiceOrderDetailDomainRules.RestoreSourceSnapshot(existingItem, sourceSnapshot);
                var updatedItem = await _serviceOrderDetailService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return ServiceOrderDetailCommandResult<ServiceOrderDetailResponseDto>.NotFound();
                }

                var responseDto = _serviceOrderDetailService.MapToDto(updatedItem);
                return ServiceOrderDetailCommandResult<ServiceOrderDetailResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return ServiceOrderDetailCommandResult<ServiceOrderDetailResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<ServiceOrderDetailCommandResult> DeleteAsync(long id, CancellationToken ct = default)
        {
            try
            {
                var existingItem = await _serviceOrderDetailService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return ServiceOrderDetailCommandResult.NotFound();
                }

                await _serviceOrderService.EnsureWorkshopEditableAsync(existingItem.ServiceOrderId);
                ServiceOrderDetailDomainRules.EnsureNoMutationForAppliedApprovedDetail(existingItem);

                var result = await _serviceOrderDetailService.DeleteAsync(id);
                if (!result)
                {
                    return ServiceOrderDetailCommandResult.NotFound();
                }

                return ServiceOrderDetailCommandResult.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return ServiceOrderDetailCommandResult.BadRequest(ex.Message);
            }
        }
    }
}
