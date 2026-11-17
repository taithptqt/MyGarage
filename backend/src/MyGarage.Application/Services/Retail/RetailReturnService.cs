using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class RetailReturnService : GenericService<RetailReturn>, IRetailReturnService
    {
        private readonly IRetailReturnDataService _dataService;
        private readonly IMapper _mapper;

        public RetailReturnService(
            IRetailReturnRepository repository,
            IRetailReturnDataService dataService,
            IMapper mapper) : base(repository)
        {
            _dataService = dataService;
            _mapper = mapper;
        }

        public List<RetailReturnResponseDto> MapToDtos(IEnumerable<RetailReturn> items)
        {
            return _mapper.Map<List<RetailReturnResponseDto>>(items);
        }

        public RetailReturnResponseDto MapToDto(RetailReturn item)
        {
            return _mapper.Map<RetailReturnResponseDto>(item);
        }

        public async Task<RetailReturnResponseDto> CreatePendingDtoAsync(RetailReturnCreateDto itemDto, long actorId)
        {
            var entity = _mapper.Map<RetailReturn>(itemDto);
            entity.Status = nameof(RetailReturnStatusCode.PENDING);
            entity.CreatedBy = actorId;
            entity.UpdatedBy = null;
            entity.ApprovedBy = null;
            entity.ApprovedAt = null;
            entity.TotalRefundAmount = null;
            var createdItem = await AddAsync(entity);
            return _mapper.Map<RetailReturnResponseDto>(createdItem);
        }

        public async Task<(IReadOnlyList<RetailReturnResponseDto> Items, int Total)> GetScopedPagedDtosAsync(
            long? branchId,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var (paged, total) = await _dataService.GetPagedAsync(branchId, page, pageSize, ct);
            return (MapToDtos(paged), total);
        }

        public async Task<RetailReturnResponseDto?> GetScopedDtoByIdAsync(
            long id,
            long? branchId,
            CancellationToken ct = default)
        {
            var item = await _dataService.GetByIdAsync(id, branchId, ct);
            return item == null ? null : MapToDto(item);
        }

        public async Task<RetailReturnCommandResult<RetailReturnResponseDto>> CreateScopedPendingAsync(
            RetailReturnCreateDto itemDto,
            long actorId,
            long? branchId,
            CancellationToken ct = default)
        {
            if (!await _dataService.RetailOrderExistsAsync(itemDto.RetailOrderId, branchId, ct))
            {
                return new RetailReturnCommandResult<RetailReturnResponseDto>(RetailReturnMutationStatus.NotFound);
            }

            if (!string.IsNullOrWhiteSpace(itemDto.Status) &&
                !string.Equals(itemDto.Status, nameof(RetailReturnStatusCode.PENDING), StringComparison.OrdinalIgnoreCase))
            {
                return new RetailReturnCommandResult<RetailReturnResponseDto>(
                    RetailReturnMutationStatus.BadRequest,
                    ErrorMessage: "Phiếu trả hàng mới phải ở trạng thái chờ xử lý.");
            }

            var responseDto = await CreatePendingDtoAsync(itemDto, actorId);
            return new RetailReturnCommandResult<RetailReturnResponseDto>(
                RetailReturnMutationStatus.Created,
                Data: responseDto);
        }

        public async Task<RetailReturnCommandResult<RetailReturnResponseDto>> UpdateScopedPendingAsync(
            long id,
            RetailReturnUpdateDto itemDto,
            long? actorId,
            long? branchId,
            CancellationToken ct = default)
        {
            var existingItem = await _dataService.GetForUpdateAsync(id, branchId, ct);
            if (existingItem == null)
            {
                return new RetailReturnCommandResult<RetailReturnResponseDto>(RetailReturnMutationStatus.NotFound);
            }

            if (!string.Equals(existingItem.Status, nameof(RetailReturnStatusCode.PENDING), StringComparison.OrdinalIgnoreCase))
            {
                return new RetailReturnCommandResult<RetailReturnResponseDto>(
                    RetailReturnMutationStatus.BadRequest,
                    ErrorMessage: "Chỉ có thể chỉnh sửa phiếu trả hàng đang chờ xử lý.");
            }

            if (itemDto.RetailOrderId != existingItem.RetailOrderId ||
                (!string.IsNullOrWhiteSpace(itemDto.Status) &&
                 !string.Equals(itemDto.Status, existingItem.Status, StringComparison.OrdinalIgnoreCase)))
            {
                return new RetailReturnCommandResult<RetailReturnResponseDto>(
                    RetailReturnMutationStatus.BadRequest,
                    ErrorMessage: "Không thể thay đổi đơn hoặc trạng thái qua chức năng chỉnh sửa.");
            }

            existingItem.Reason = itemDto.Reason;
            existingItem.ReturnDate = itemDto.ReturnDate;
            existingItem.UpdatedBy = actorId;
            var updatedItem = await UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return new RetailReturnCommandResult<RetailReturnResponseDto>(RetailReturnMutationStatus.NotFound);
            }

            return new RetailReturnCommandResult<RetailReturnResponseDto>(
                RetailReturnMutationStatus.Ok,
                Data: MapToDto(updatedItem));
        }

        public async Task<RetailReturnCommandResult<bool>> DeleteScopedPendingAsync(
            long id,
            long? branchId,
            CancellationToken ct = default)
        {
            var existing = await _dataService.GetByIdAsync(id, branchId, ct);
            if (existing == null)
            {
                return new RetailReturnCommandResult<bool>(RetailReturnMutationStatus.NotFound);
            }

            if (!string.Equals(existing.Status, nameof(RetailReturnStatusCode.PENDING), StringComparison.OrdinalIgnoreCase))
            {
                return new RetailReturnCommandResult<bool>(
                    RetailReturnMutationStatus.BadRequest,
                    ErrorMessage: "Chỉ có thể xóa phiếu trả hàng đang chờ xử lý.");
            }

            var deleted = await DeleteAsync(id);
            return deleted
                ? new RetailReturnCommandResult<bool>(RetailReturnMutationStatus.Ok, Data: true)
                : new RetailReturnCommandResult<bool>(RetailReturnMutationStatus.NotFound);
        }

        public Task<RetailReturnFinalizeResult> FinalizeReturnAsync(
            long id,
            long actorUserId,
            long? scopedBranch,
            CancellationToken ct = default)
            => _dataService.FinalizeReturnAsync(id, actorUserId, scopedBranch, ct);
    }
}
