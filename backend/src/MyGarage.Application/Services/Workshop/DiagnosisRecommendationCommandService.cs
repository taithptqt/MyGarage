using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Interfaces.Notifications;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class DiagnosisRecommendationCommandService : IDiagnosisRecommendationCommandService
    {
        private readonly IDiagnosisRecommendationService _diagnosisRecommendationService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IDiagnosisRecommendationDataService _dataService;
        private readonly IDiagnosisRealtimeNotifier _realtimeNotifier;

        public DiagnosisRecommendationCommandService(
            IDiagnosisRecommendationService diagnosisRecommendationService,
            IServiceOrderService serviceOrderService,
            IDiagnosisRecommendationDataService dataService,
            IDiagnosisRealtimeNotifier realtimeNotifier)
        {
            _diagnosisRecommendationService = diagnosisRecommendationService ?? throw new ArgumentNullException(nameof(diagnosisRecommendationService));
            _serviceOrderService = serviceOrderService ?? throw new ArgumentNullException(nameof(serviceOrderService));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _realtimeNotifier = realtimeNotifier ?? throw new ArgumentNullException(nameof(realtimeNotifier));
        }

        public async Task<PagedEnvelope<DiagnosisRecommendationResponseDto>> GetAllAsync(
            long? diagnosisId,
            long? diagnosisAssignmentId,
            int? page = null,
            int? pageSize = null,
            CancellationToken ct = default)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (items, total) = await _dataService.GetPagedAsync(
                diagnosisId,
                diagnosisAssignmentId,
                normalizedPage,
                normalizedSize,
                ct);
            var dtos = _diagnosisRecommendationService.MapToDtos(items);
            return PagedEnvelope<DiagnosisRecommendationResponseDto>.Create(dtos, total, normalizedPage, normalizedSize);
        }

        public Task<IReadOnlyList<PendingSupplementaryProposalResponseDto>> GetPendingSupplementaryAsync(
            long? serviceOrderId = null,
            long? branchId = null,
            CancellationToken ct = default)
        {
            return _dataService.GetPendingSupplementaryAsync(serviceOrderId, branchId, ct);
        }

        public Task<DiagnosisRecommendationResponseDto?> GetByIdAsync(long id, CancellationToken ct = default)
        {
            return _diagnosisRecommendationService.GetDtoByIdAsync(id);
        }

        public async Task<DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>> CreateAsync(
            DiagnosisRecommendationCreateDto? itemDto,
            CancellationToken ct = default)
        {
            try
            {
                if (itemDto == null || (!itemDto.DiagnosisAssignmentId.HasValue && !itemDto.DiagnosisId.HasValue))
                {
                    return DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>.BadRequest(
                        "Thiếu DiagnosisAssignmentId hoặc DiagnosisId hợp lệ để tạo kiến nghị kỹ thuật.");
                }

                var diagnosisLink = await _dataService.ResolveDiagnosisLinkAsync(
                    itemDto.DiagnosisAssignmentId,
                    itemDto.DiagnosisId,
                    ct);
                if (!diagnosisLink.HasValue)
                {
                    return DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>.BadRequest(
                        "Không tìm thấy vòng phân công chuẩn đoán hợp lệ.");
                }

                var serviceOrderId = await _dataService.ResolveServiceOrderIdFromDiagnosisIdAsync(
                    diagnosisLink.Value.DiagnosisId,
                    ct);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(serviceOrderId);
                }

                var entity = _diagnosisRecommendationService.MapToEntity(itemDto);
                entity.DiagnosisId = diagnosisLink.Value.DiagnosisId;
                entity.DiagnosisAssignmentId = diagnosisLink.Value.DiagnosisAssignmentId;
                entity.RecommendationStatus = DiagnosisRecommendationDomainRules.ResolveInitialStatus(entity.RecommendationStatus);

                var evidenceCheck = DiagnosisRecommendationDomainRules.ValidateEvidenceRequirement(entity);
                if (!evidenceCheck.IsValid)
                {
                    return DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>.BadRequest(
                        evidenceCheck.ErrorMessage ?? DiagnosisRecommendationDomainRules.EvidenceRequiredMessage);
                }

                var createdItem = await _diagnosisRecommendationService.AddAsync(entity);
                await NotifySupplementaryProposalToStaffAsync(
                    createdItem.DiagnosisId,
                    createdItem.Id,
                    "CREATED",
                    createdItem.CreatedBy,
                    ct);
                var responseDto = _diagnosisRecommendationService.MapToDto(createdItem);
                return DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>> UpdateAsync(
            long id,
            DiagnosisRecommendationUpdateDto? itemDto,
            CancellationToken ct = default)
        {
            try
            {
                if (itemDto == null)
                {
                    return DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>.BadRequest(
                        "Dữ liệu cập nhật không hợp lệ.");
                }

                var existingItem = await _diagnosisRecommendationService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>.NotFound();
                }

                var diagnosisLink = await _dataService.ResolveDiagnosisLinkAsync(
                    itemDto.DiagnosisAssignmentId ?? existingItem.DiagnosisAssignmentId,
                    itemDto.DiagnosisId ?? existingItem.DiagnosisId,
                    ct);
                if (!diagnosisLink.HasValue)
                {
                    return DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>.BadRequest(
                        "Không tìm thấy vòng phân công chuẩn đoán hợp lệ.");
                }

                var serviceOrderId = await _dataService.ResolveServiceOrderIdFromDiagnosisIdAsync(
                    diagnosisLink.Value.DiagnosisId,
                    ct);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(serviceOrderId);
                }

                var originalQuantity = existingItem.Quantity;
                _diagnosisRecommendationService.ApplyUpdate(itemDto, existingItem);
                existingItem.DiagnosisId = diagnosisLink.Value.DiagnosisId;
                existingItem.DiagnosisAssignmentId = diagnosisLink.Value.DiagnosisAssignmentId;

                var evidenceCheck = DiagnosisRecommendationDomainRules.ValidateEvidenceRequirement(existingItem);
                if (!evidenceCheck.IsValid)
                {
                    return DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>.BadRequest(
                        evidenceCheck.ErrorMessage ?? DiagnosisRecommendationDomainRules.EvidenceRequiredMessage);
                }

                var updatedItem = await _diagnosisRecommendationService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>.NotFound();
                }

                await NotifySupplementaryProposalToStaffAsync(
                    updatedItem.DiagnosisId,
                    updatedItem.Id,
                    "UPDATED",
                    updatedItem.UpdatedBy,
                    ct);

                var hasQuantityChanged = Math.Abs(updatedItem.Quantity - originalQuantity) > 0.000001m;
                if (hasQuantityChanged)
                {
                    await _dataService.SyncChildMaterialQuantitiesByBomAsync(updatedItem, ct);
                }

                var responseDto = _diagnosisRecommendationService.MapToDto(updatedItem);
                return DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>.Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<DiagnosisRecommendationCommandResult> DeleteAsync(long id, CancellationToken ct = default)
        {
            try
            {
                var existingItem = await _diagnosisRecommendationService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return DiagnosisRecommendationCommandResult.NotFound();
                }

                var serviceOrderId = existingItem.DiagnosisAssignmentId.HasValue
                    ? await _dataService.ResolveServiceOrderIdFromAssignmentIdAsync(existingItem.DiagnosisAssignmentId.Value, ct)
                    : await _dataService.ResolveServiceOrderIdFromDiagnosisIdAsync(existingItem.DiagnosisId, ct);

                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(serviceOrderId);
                }

                DiagnosisRecommendationDomainRules.MarkSoftDeleted(
                    existingItem,
                    VietnamTime.Now,
                    existingItem.UpdatedBy);

                var updatedItem = await _diagnosisRecommendationService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return DiagnosisRecommendationCommandResult.NotFound();
                }

                return DiagnosisRecommendationCommandResult.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return DiagnosisRecommendationCommandResult.BadRequest(ex.Message);
            }
        }

        private async Task NotifySupplementaryProposalToStaffAsync(
            long diagnosisId,
            long recommendationId,
            string action,
            long? actorStaffId,
            CancellationToken ct)
        {
            if (diagnosisId <= 0 || recommendationId <= 0)
            {
                return;
            }

            var target = await _dataService.GetSupplementaryNotificationTargetAsync(diagnosisId, ct);
            if (target == null)
            {
                return;
            }

            var recipientStaffIds = new HashSet<long>();
            if (target.ReceptionistId.HasValue && target.ReceptionistId.Value > 0)
            {
                recipientStaffIds.Add(target.ReceptionistId.Value);
            }

            if (target.ServiceAdvisorId.HasValue && target.ServiceAdvisorId.Value > 0)
            {
                recipientStaffIds.Add(target.ServiceAdvisorId.Value);
            }

            if (actorStaffId.HasValue && actorStaffId.Value > 0)
            {
                recipientStaffIds.Remove(actorStaffId.Value);
            }

            if (recipientStaffIds.Count == 0)
            {
                return;
            }

            await _realtimeNotifier.NotifySupplementaryProposalUpdatedAsync(
                recipientStaffIds,
                new SupplementaryProposalRealtimeEvent(
                    action ?? "UPDATED",
                    target.ServiceOrderId,
                    target.Code,
                    diagnosisId,
                    recommendationId,
                    target.ExecuteBranchId,
                    target.OriginBranchId,
                    VietnamTime.Now),
                ct);
        }
    }
}
