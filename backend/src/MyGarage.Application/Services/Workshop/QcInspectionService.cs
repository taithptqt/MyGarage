using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class QcInspectionService : GenericService<QcInspection>, IQcInspectionService
    {
        private const string QcResultPass = "PASS";
        private const string QcResultFail = "FAIL";
        private const string QcResultNa = "N/A";
        private const string SelfInspectionForbiddenMessage =
            "Kỹ thuật viên không được phép tự nghiệm thu (QC) công việc của chính mình (Separation of Duties).";

        private readonly IQcInspectionStore _store;
        private readonly IQcInspectionDataService _dataService;
        private readonly IMapper _mapper;

        public QcInspectionService(
            IQcInspectionRepository repository,
            IQcInspectionStore store,
            IQcInspectionDataService dataService,
            IMapper mapper) : base(repository)
        {
            _store = store;
            _dataService = dataService;
            _mapper = mapper;
        }

        public List<QcInspectionResponseDto> MapToDtos(IEnumerable<QcInspection> items)
        {
            return _mapper.Map<List<QcInspectionResponseDto>>(items);
        }

        public async Task<QcInspectionResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<QcInspectionResponseDto>(item);
        }

        public async Task<(IReadOnlyList<QcInspectionResponseDto> Items, int Total)> GetPagedDtosAsync(
            long? serviceOrderId,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                var (paged, total) = await _dataService.GetPagedByServiceOrderAsync(serviceOrderId.Value, page, pageSize, ct);
                return (MapToDtos(paged), total);
            }

            var (allPaged, allTotal) = await GetPageByIdAsync(page, pageSize);
            return (MapToDtos(allPaged), allTotal);
        }

        public async Task<QcInspectionResponseDto> CreateDtoAsync(QcInspectionCreateDto itemDto)
        {
            if (itemDto.QcStaffId > 0 && await _dataService.IsTechnicianWhoWorkedOnOrderAsync(itemDto.ServiceOrderId, itemDto.QcStaffId))
            {
                throw new InvalidOperationException(SelfInspectionForbiddenMessage);
            }

            var entity = _mapper.Map<QcInspection>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<QcInspectionResponseDto>(createdItem);
        }

        public async Task<QcMutationResult<QcInspectionResponseDto>> CreateWithSeparationOfDutiesAsync(
            QcInspectionCreateDto itemDto,
            long? actorUserId,
            CancellationToken ct = default)
        {
            var inspectorId = itemDto.QcStaffId > 0
                ? itemDto.QcStaffId
                : actorUserId ?? 0;

            if (inspectorId > 0 && await _dataService.IsTechnicianWhoWorkedOnOrderAsync(itemDto.ServiceOrderId, inspectorId, ct))
            {
                return new QcMutationResult<QcInspectionResponseDto>(
                    QcMutationStatus.BadRequest,
                    ErrorMessage: SelfInspectionForbiddenMessage);
            }

            var entity = _mapper.Map<QcInspection>(itemDto);
            var createdItem = await AddAsync(entity);
            return new QcMutationResult<QcInspectionResponseDto>(
                QcMutationStatus.Created,
                Data: _mapper.Map<QcInspectionResponseDto>(createdItem));
        }

        public async Task<QcInspectionResponseDto?> UpdateDtoAsync(long id, QcInspectionUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<QcInspectionResponseDto>(updatedItem);
        }

        public async Task<QcMutationResult<object>> UpsertChecklistItemsAsync(
            long id,
            QcInspectionItemsUpsertRequestDto? request,
            long? actorUserId,
            CancellationToken ct = default)
        {
            request ??= new QcInspectionItemsUpsertRequestDto();
            var serviceOrderId = await _dataService.GetServiceOrderIdAsync(id, ct);
            if (!serviceOrderId.HasValue)
            {
                return new QcMutationResult<object>(
                    QcMutationStatus.NotFound,
                    ErrorMessage: $"QC inspection #{id} not found.");
            }

            var currentUserId = actorUserId ?? 0;
            if (currentUserId > 0 && await _dataService.IsTechnicianWhoWorkedOnOrderAsync(serviceOrderId.Value, currentUserId, ct))
            {
                return new QcMutationResult<object>(
                    QcMutationStatus.BadRequest,
                    ErrorMessage: SelfInspectionForbiddenMessage);
            }

            var incomingItems = request.Items ?? [];
            if (!request.OverwriteExisting && incomingItems.Count == 0)
            {
                return new QcMutationResult<object>(
                    QcMutationStatus.BadRequest,
                    ErrorMessage: "items is required when overwriteExisting=false.");
            }

            var validatedItems = new List<(QcInspectionChecklistItemUpsertDto Input, string ItemName, string? NormalizedResult)>(incomingItems.Count);
            foreach (var dto in incomingItems)
            {
                var itemName = (dto.ItemName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    return new QcMutationResult<object>(
                        QcMutationStatus.BadRequest,
                        ErrorMessage: "Each checklist item requires itemName.");
                }

                var normalizedResult = NormalizeQcResult(dto.Result);
                if (dto.Result is not null && normalizedResult is null)
                {
                    return new QcMutationResult<object>(
                        QcMutationStatus.BadRequest,
                        ErrorMessage: "result must be PASS, FAIL, N/A or empty.");
                }

                validatedItems.Add((dto, itemName, normalizedResult));
            }

            var summary = await _dataService.UpsertChecklistItemsAsync(
                id,
                request.OverwriteExisting,
                validatedItems,
                ct);
            if (summary is null)
            {
                return new QcMutationResult<object>(
                    QcMutationStatus.NotFound,
                    ErrorMessage: $"QC inspection #{id} not found.");
            }

            return new QcMutationResult<object>(QcMutationStatus.Ok, Data: summary);
        }

        public async Task<bool> PassAsync(long id, QcPassRequestDto request)
        {
            var result = await PassAsync(id, request, actorUserId: null, CancellationToken.None);
            if (result.Status == QcMutationStatus.BadRequest)
            {
                throw new InvalidOperationException(result.ErrorMessage);
            }

            return result.Data;
        }

        public async Task<QcMutationResult<bool>> PassAsync(
            long id,
            QcPassRequestDto request,
            long? actorUserId,
            CancellationToken ct = default)
        {
            var inspection = await _store.GetInspectionForUpdateAsync(id, ct);
            if (inspection == null)
            {
                return new QcMutationResult<bool>(QcMutationStatus.NotFound, Data: false);
            }

            if (actorUserId is > 0 && request.InspectorId > 0 && request.InspectorId != actorUserId.Value)
            {
                return new QcMutationResult<bool>(QcMutationStatus.BadRequest, Data: false, ErrorMessage: "Nhân sự kiểm tra không khớp tài khoản đang đăng nhập.");
            }
            var inspectorId = actorUserId is > 0 ? actorUserId.Value : request.InspectorId;
            if (inspectorId > 0 && await _dataService.IsTechnicianWhoWorkedOnOrderAsync(inspection.ServiceOrderId, inspectorId, ct))
            {
                return new QcMutationResult<bool>(QcMutationStatus.BadRequest, Data: false, ErrorMessage: SelfInspectionForbiddenMessage);
            }

            var orderUnavailable = false;
            await _store.ExecuteInTransactionAsync(async () =>
            {
                var order = await _store.GetServiceOrderForUpdateAsync(inspection.ServiceOrderId, ct);
                if (order == null || string.Equals(order.Status, nameof(ServiceOrderLifecycleStatus.CLOSED), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(order.Status, nameof(ServiceOrderLifecycleStatus.CANCELED), StringComparison.OrdinalIgnoreCase))
                {
                    orderUnavailable = true;
                    return;
                }
                inspection.Status = nameof(QcInspectionStatusCode.PASS);
                inspection.InspectedAt = VietnamTime.Now;
                inspection.QcStaffId = inspectorId;
                inspection.OverallNote = request.Notes;
                await _repository.UpdateAsync(id, inspection);

                if (order != null)
                {
                    order.Qcstatus = nameof(ServiceOrderQcStatusCode.PASS);
                    order.QcverifiedAt = inspection.InspectedAt;
                    order.QcinspectorId = inspectorId;

                    // Automatically move to READY for handover if QC passes
                    order.Status = nameof(ServiceOrderLifecycleStatus.READY);
                    order.UpdatedAt = VietnamTime.Now;
                }

                await _store.SaveChangesAsync(ct);
            }, ct);

            return orderUnavailable
                ? new QcMutationResult<bool>(QcMutationStatus.BadRequest, Data: false, ErrorMessage: "Không thể kiểm tra chất lượng lệnh đã đóng, đã hủy hoặc không tồn tại.")
                : new QcMutationResult<bool>(QcMutationStatus.Ok, Data: true);
        }

        public async Task<bool> FailAsync(long id, QcFailRequestDto request)
        {
            var result = await FailAsync(id, request, actorUserId: null, CancellationToken.None);
            if (result.Status == QcMutationStatus.BadRequest)
            {
                throw new InvalidOperationException(result.ErrorMessage);
            }

            return result.Data;
        }

        public async Task<QcMutationResult<bool>> FailAsync(
            long id,
            QcFailRequestDto request,
            long? actorUserId,
            CancellationToken ct = default)
        {
            var inspection = await _store.GetInspectionForUpdateAsync(id, ct);
            if (inspection == null)
            {
                return new QcMutationResult<bool>(QcMutationStatus.NotFound, Data: false);
            }

            if (actorUserId is > 0 && request.InspectorId > 0 && request.InspectorId != actorUserId.Value)
            {
                return new QcMutationResult<bool>(QcMutationStatus.BadRequest, Data: false, ErrorMessage: "Nhân sự kiểm tra không khớp tài khoản đang đăng nhập.");
            }
            var inspectorId = actorUserId is > 0 ? actorUserId.Value : request.InspectorId;
            if (inspectorId > 0 && await _dataService.IsTechnicianWhoWorkedOnOrderAsync(inspection.ServiceOrderId, inspectorId, ct))
            {
                return new QcMutationResult<bool>(QcMutationStatus.BadRequest, Data: false, ErrorMessage: SelfInspectionForbiddenMessage);
            }

            var orderUnavailable = false;
            await _store.ExecuteInTransactionAsync(async () =>
            {
                var order = await _store.GetServiceOrderForUpdateAsync(inspection.ServiceOrderId, ct);
                if (order == null || string.Equals(order.Status, nameof(ServiceOrderLifecycleStatus.CLOSED), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(order.Status, nameof(ServiceOrderLifecycleStatus.CANCELED), StringComparison.OrdinalIgnoreCase))
                {
                    orderUnavailable = true;
                    return;
                }
                inspection.Status = nameof(QcInspectionStatusCode.FAIL);
                inspection.InspectedAt = VietnamTime.Now;
                inspection.QcStaffId = inspectorId;
                inspection.OverallNote = request.Reason;
                await _repository.UpdateAsync(id, inspection);

                if (order != null)
                {
                    order.Qcstatus = nameof(ServiceOrderQcStatusCode.FAIL);
                    order.QcinspectorId = inspectorId;

                    // Revert to REPAIRING status on QC failure
                    order.Status = nameof(ServiceOrderLifecycleStatus.REPAIRING);
                    order.UpdatedAt = VietnamTime.Now;
                }

                await _store.SaveChangesAsync(ct);
            }, ct);

            return orderUnavailable
                ? new QcMutationResult<bool>(QcMutationStatus.BadRequest, Data: false, ErrorMessage: "Không thể kiểm tra chất lượng lệnh đã đóng, đã hủy hoặc không tồn tại.")
                : new QcMutationResult<bool>(QcMutationStatus.Ok, Data: true);
        }

        private static string? NormalizeQcResult(string? result)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                return null;
            }

            var normalized = result.Trim().ToUpperInvariant();
            return normalized switch
            {
                "PASS" => QcResultPass,
                "FAIL" => QcResultFail,
                "NA" => QcResultNa,
                "N/A" => QcResultNa,
                _ => null
            };
        }
    }
}
