using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class QuotationBuildService : IQuotationBuildService
{
    private readonly IQuotationBuildStore _buildStore;
    private readonly IQuotationApprovalStore _approvalStore;
    private readonly IQuotationLineageQueryStore _lineageStore;
    private readonly IQuotationQueryService _queryService;

    public QuotationBuildService(
        IQuotationBuildStore buildStore,
        IQuotationApprovalStore approvalStore,
        IQuotationLineageQueryStore lineageStore,
        IQuotationQueryService queryService)
    {
        _buildStore = buildStore;
        _approvalStore = approvalStore;
        _lineageStore = lineageStore;
        _queryService = queryService;
    }

    public async Task<QuotationResponseDto> CreateQuotationAsync(long serviceOrderId, QuotationCreateRequestDto request, long? actorStaffId = null)
    {
        if (serviceOrderId <= 0)
        {
            throw new InvalidOperationException("Service order không hợp lệ.");
        }

        if (request == null || request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("Báo giá phải có ít nhất 1 hạng mục.");
        }

        var serviceOrder = await _buildStore.GetServiceOrderAdvisorInfoAsync(serviceOrderId);
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
        }

        long? resolvedDiagnosisId = request.DiagnosisId.HasValue && request.DiagnosisId.Value > 0
            ? request.DiagnosisId.Value
            : null;
        long? resolvedDiagnosisAssignmentId = request.DiagnosisAssignmentId.HasValue && request.DiagnosisAssignmentId.Value > 0
            ? request.DiagnosisAssignmentId.Value
            : null;

        if (resolvedDiagnosisAssignmentId.HasValue)
        {
            var assignmentInfo = await _buildStore.GetDiagnosisAssignmentInfoAsync(resolvedDiagnosisAssignmentId.Value);
            if (assignmentInfo == null || assignmentInfo.DiagnosisServiceOrderId != serviceOrderId)
            {
                throw new InvalidOperationException("DiagnosisAssignmentId không thuộc lệnh sửa chữa hiện tại.");
            }

            if (resolvedDiagnosisId.HasValue && resolvedDiagnosisId.Value != assignmentInfo.DiagnosisId)
            {
                throw new InvalidOperationException("DiagnosisId không khớp với DiagnosisAssignmentId.");
            }

            resolvedDiagnosisId ??= assignmentInfo.DiagnosisId;
        }

        if (resolvedDiagnosisId.HasValue)
        {
            var diagnosisBelongsToServiceOrder = await _buildStore.DiagnosisBelongsToServiceOrderAsync(resolvedDiagnosisId.Value, serviceOrderId);
            if (!diagnosisBelongsToServiceOrder)
            {
                throw new InvalidOperationException("DiagnosisId không thuộc lệnh sửa chữa hiện tại.");
            }

            if (!resolvedDiagnosisAssignmentId.HasValue)
            {
                resolvedDiagnosisAssignmentId = await _buildStore.GetLatestDiagnosisAssignmentIdAsync(resolvedDiagnosisId.Value);
            }
        }

        if (!resolvedDiagnosisAssignmentId.HasValue || resolvedDiagnosisAssignmentId.Value <= 0)
        {
            throw new InvalidOperationException("Báo giá phải gắn với một phiên bản chuẩn đoán hợp lệ (DiagnosisAssignmentId).");
        }

        return await _buildStore.ExecuteCreateQuotationTransactionalAsync(serviceOrderId, async () =>
        {
            var nextVersion = await _buildStore.GetMaxQuotationVersionNoAsync(serviceOrderId) + 1;

            var now = VietnamTime.Now;
            var normalizedType = QuotationValidationHelper.NormalizeQuotationType(request.QuotationType);
            var quotation = new Quotation
            {
                Code = QuotationValidationHelper.BuildQuotationCode(serviceOrderId, nextVersion, now),
                ServiceOrderId = serviceOrderId,
                DiagnosisId = resolvedDiagnosisId,
                DiagnosisAssignmentId = resolvedDiagnosisAssignmentId,
                Title = QuotationValidationHelper.BuildQuotationTitle(nextVersion),
                QuotationType = normalizedType,
                VersionNo = nextVersion,
                Status = request.SubmitForApproval ? QuotationWorkflowConstants.StatusWaitingApproval : QuotationWorkflowConstants.StatusDraft,
                InsuranceClaimNumber = QuotationValidationHelper.NormalizeText(request.InsuranceClaimNumber, 100),
                ValidUntil = request.ValidUntil,
                ServiceAdvisorId = QuotationValidationHelper.ResolveServiceAdvisorForQuotation(
                    request.ServiceAdvisorId,
                    serviceOrder.ServiceAdvisorId,
                    serviceOrder.ReceptionistId),
                CustomerNotes = QuotationValidationHelper.NormalizeText(request.CustomerNotes, 1000),
                InternalNotes = QuotationValidationHelper.NormalizeText(request.InternalNotes, 1000),
                TechnicalApprovalStatus = QuotationWorkflowConstants.TechnicalApprovalPending,
                RequiresFinancialApproval = false,
                FinancialApprovalStatus = QuotationWorkflowConstants.FinancialApprovalNotRequired,
                CustomerDecisionStatus = QuotationWorkflowConstants.ApprovalPending,
                CreatedBy = actorStaffId,
                UpdatedBy = actorStaffId,
                CreatedAt = now,
                UpdatedAt = now
            };

            var normalizedItems = (request.Items ?? new List<QuotationCreateItemDto>())
                .Where(x => x != null)
                .ToList();
            var details = normalizedItems
                .Select(x => QuotationMappingHelper.BuildQuotationDetailEntity(x, serviceOrderId, actorStaffId, now))
                .ToList();
            var discountAppliedBy = actorStaffId
                ?? serviceOrder.ServiceAdvisorId
                ?? serviceOrder.ReceptionistId
                ?? 0;

            if (details.Count == 0)
            {
                throw new InvalidOperationException("Báo giá không có hạng mục hợp lệ.");
            }

            quotation.QuotationDetails = details;
            quotation.QuotationDiscounts = QuotationPricingHelper.BuildLineManualDiscountsFromCreateItems(
                quotation,
                normalizedItems,
                details,
                discountAppliedBy,
                now,
                request.InternalNotes);
            QuotationPricingHelper.RecalculateQuotationTotals(quotation);
            QuotationValidationHelper.ValidateQuotationSplitBilling(quotation);
            quotation.InsuranceChangeType = await QuotationValidationHelper.ResolveInsuranceChangeTypeAsync(_lineageStore, quotation, null);

            var priorOpenQuotations = await _buildStore.GetPriorOpenQuotationsForUpdateAsync(
                serviceOrderId,
                QuotationWorkflowConstants.StatusSuperseded,
                QuotationWorkflowConstants.StatusCustomerApproved);

            foreach (var row in priorOpenQuotations)
            {
                row.Status = QuotationWorkflowConstants.StatusSuperseded;
                row.UpdatedAt = now;
                row.UpdatedBy = actorStaffId;
            }

            _buildStore.AddQuotation(quotation);
            await _buildStore.SaveChangesInTransactionAsync();
            return QuotationMappingHelper.MapQuotationResponse(quotation);
        });
    }

    public async Task<QuotationResponseDto> GenerateInitialQuotationFromDiagnosisAsync(long serviceOrderId, long actorStaffId, string? note = null, IReadOnlyList<long>? diagnosisIds = null)
    {
        if (serviceOrderId <= 0)
        {
            throw new InvalidOperationException("Service order is invalid.");
        }

        if (actorStaffId <= 0)
        {
            throw new InvalidOperationException("Actor staff is required.");
        }

        var serviceOrder = await _buildStore.GetServiceOrderAsNoTrackingAsync(serviceOrderId);
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Service order not found.");
        }

        var diagnosisIdFilter = (diagnosisIds ?? Array.Empty<long>()).Where(x => x > 0).Distinct().ToList();

        var diagnoses = await _buildStore.GetNonCanceledDiagnosesAsync(serviceOrderId, diagnosisIdFilter);
        if (diagnoses.Count == 0)
        {
            throw new InvalidOperationException("No diagnosis found to create quotation.");
        }

        var sourceDiagnosisIds = diagnoses.Select(x => x.Id).Distinct().ToList();
        var latestAssignmentIdsByDiagnosis = await _buildStore.GetDiagnosisAssignmentsOrderedAsync(sourceDiagnosisIds);
        var latestAssignmentByDiagnosisId = latestAssignmentIdsByDiagnosis
            .GroupBy(x => x.DiagnosisId)
            .ToDictionary(x => x.Key, x => x.First().Id);
        var latestAssignmentIds = latestAssignmentByDiagnosisId.Values.ToList();

        var recommendations = await _buildStore.GetRecommendationsByAssignmentsOrDiagnosesAsync(
            latestAssignmentIds,
            sourceDiagnosisIds,
            nameof(RecommendationLifecycleStatusCode.SOFT_DELETED_BY_TL));
        if (recommendations.Count == 0)
        {
            recommendations = await _buildStore.GetRecommendationsByDiagnosesAsync(
                sourceDiagnosisIds,
                nameof(RecommendationLifecycleStatusCode.SOFT_DELETED_BY_TL));
        }
        if (recommendations.Count == 0)
        {
            throw new InvalidOperationException("No recommendation found to create quotation.");
        }

        var standardServiceIds = recommendations
            .Where(x => x.StandardServiceId.HasValue && x.StandardServiceId.Value > 0)
            .Select(x => x.StandardServiceId!.Value)
            .Distinct()
            .ToList();
        var productIds = recommendations
            .Where(x => x.InventoryItemId.HasValue && x.InventoryItemId.Value > 0)
            .Select(x => x.InventoryItemId!.Value)
            .Distinct()
            .ToList();

        var serviceById = await _buildStore.GetStandardServicesMapAsync(standardServiceIds);
        var productById = await _buildStore.GetProductsMapAsync(productIds);

        return await _buildStore.ExecuteTransactionalAsync<QuotationResponseDto>(async () =>
        {
            var latestDiagnosis = diagnoses.First();
            var now = VietnamTime.Now;

            var request = new QuotationCreateRequestDto
            {
                DiagnosisId = latestDiagnosis.Id,
                DiagnosisAssignmentId = latestAssignmentByDiagnosisId.TryGetValue(latestDiagnosis.Id, out var latestAssignmentId)
                    ? latestAssignmentId
                    : null,
                Title = null,
                QuotationType = nameof(QuotationTypeCode.INSURANCE),
                ServiceAdvisorId = QuotationValidationHelper.ResolveServiceAdvisorForQuotation(
                    null,
                    serviceOrder.ServiceAdvisorId,
                    serviceOrder.ReceptionistId),
                InternalNotes = note,
                SubmitForApproval = false
            };

            foreach (var rec in recommendations.OrderBy(x => x.Id))
            {
                var mapped = QuotationPricingHelper.MapRecommendationToPrice(rec, serviceById, productById);
                request.Items.Add(new QuotationCreateItemDto
                {
                    DiagnosisRecommendationId = rec.Id,
                    ItemType = mapped.ItemType,
                    ItemId = mapped.ItemId,
                    ItemName = mapped.ItemName,
                    Description = rec.TechnicianNotes,
                    Quantity = rec.Quantity <= 0 ? 1 : rec.Quantity,
                    UnitPriceAtTime = mapped.UnitPrice,
                    CostPriceAtTime = mapped.CostPrice,
                    DiscountValue = 0,
                    TaxRate = mapped.TaxRate,
                    PayerType = "INSURANCE",
                    IsBlocking = true,
                    IsMandatory = QuotationValidationHelper.IsMandatoryByUrgency(rec.Urgency),
                    EstimatedMinutes = null,
                    IsReturnOldPart = false
                });
            }

            var quotation = await CreateQuotationAsync(serviceOrderId, request, actorStaffId);
            var quotationEntity = await _buildStore.GetQuotationRequiredForUpdateAsync(quotation.Id);
            quotationEntity.Status = QuotationWorkflowConstants.StatusPendingInsurance;
            quotationEntity.CustomerDecisionStatus = QuotationWorkflowConstants.ApprovalPending;
            quotationEntity.UpdatedBy = actorStaffId;
            quotationEntity.UpdatedAt = now;
            await _buildStore.SaveChangesInTransactionAsync();

            return await _queryService.GetQuotationAsync(quotationEntity.Id) ?? QuotationMappingHelper.MapQuotationResponse(quotationEntity);
        });
    }

    public async Task<string?> ValidateInitialQuotationFromDiagnosisAsync(long serviceOrderId, IReadOnlyList<long>? diagnosisIds = null)
    {
        if (serviceOrderId <= 0)
        {
            return "Service order is invalid.";
        }

        var serviceOrderExists = await _buildStore.ServiceOrderExistsAsync(serviceOrderId);
        if (!serviceOrderExists)
        {
            return "Service order not found.";
        }

        var diagnosisIdFilter = (diagnosisIds ?? Array.Empty<long>()).Where(x => x > 0).Distinct().ToList();

        var sourceDiagnosisIds = await _buildStore.GetNonCanceledDiagnosisIdsAsync(serviceOrderId, diagnosisIdFilter);
        if (sourceDiagnosisIds.Count == 0)
        {
            return "No diagnosis found to create quotation.";
        }

        var latestAssignments = await _buildStore.GetDiagnosisAssignmentsOrderedAsync(sourceDiagnosisIds);
        var latestAssignmentIds = latestAssignments
            .GroupBy(x => x.DiagnosisId)
            .Select(x => x.First().Id)
            .ToList();

        var hasRecommendation = await _buildStore.HasRecommendationAsync(
            latestAssignmentIds,
            sourceDiagnosisIds,
            nameof(RecommendationLifecycleStatusCode.SOFT_DELETED_BY_TL));
        if (!hasRecommendation)
        {
            hasRecommendation = await _buildStore.HasRecommendationByDiagnosesAsync(
                sourceDiagnosisIds,
                nameof(RecommendationLifecycleStatusCode.SOFT_DELETED_BY_TL));
        }

        if (!hasRecommendation)
        {
            return "No recommendation found to create quotation.";
        }

        return null;
    }

    public async Task<QuotationResponseDto> CloneQuotationAsync(long quotationId, QuotationCloneRequestDto request)
    {
        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Actor staff is required.");
        }

        var source = await _approvalStore.GetQuotationWithDetailsAndDiscountsAsync(quotationId);
        if (source == null)
        {
            throw new InvalidOperationException("Quotation not found.");
        }

        if (string.Equals(source.Status, QuotationWorkflowConstants.StatusCanceled, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot clone a cancelled quotation.");
        }

        var removedIds = (request.RemoveQuotationDetailIds ?? new List<long>())
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();

        var serviceIds = source.QuotationDetails
            .Where(x => x.ItemId.HasValue && string.Equals(QuotationMappingHelper.NormalizeServiceOrderDetailItemType(x.ItemType), "LABOR", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ItemId!.Value)
            .Distinct()
            .ToList();
        var productIds = source.QuotationDetails
            .Where(x => x.ItemId.HasValue && string.Equals(QuotationMappingHelper.NormalizeServiceOrderDetailItemType(x.ItemType), "PART", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ItemId!.Value)
            .Distinct()
            .ToList();

        var serviceById = await _buildStore.GetStandardServicesMapAsync(serviceIds);
        var productById = await _buildStore.GetProductsMapAsync(productIds);

        var cloneRequest = new QuotationCreateRequestDto
        {
            DiagnosisId = source.DiagnosisId,
            DiagnosisAssignmentId = source.DiagnosisAssignmentId,
            Title = source.Title,
            QuotationType = source.QuotationType,
            ServiceAdvisorId = source.ServiceAdvisorId,
            InsuranceClaimNumber = source.InsuranceClaimNumber,
            CustomerNotes = source.CustomerNotes,
            InternalNotes = request.Note,
            SubmitForApproval = false
        };

        var removedMandatoryRows = new List<QuotationDetail>();
        var removedInsuranceAmount = 0m;
        foreach (var detail in source.QuotationDetails.OrderBy(x => x.Id))
        {
            var markCanceled = removedIds.Contains(detail.Id);
            if (markCanceled)
            {
                if (detail.IsMandatory)
                {
                    removedMandatoryRows.Add(detail);
                }

                if (string.Equals(detail.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(detail.PayerType, "SPLIT", StringComparison.OrdinalIgnoreCase))
                {
                    removedInsuranceAmount += detail.InsuranceCoveredAmount
                        ?? (string.Equals(detail.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase) ? detail.FinalAmount : 0m);
                }
            }

            var repriced = QuotationPricingHelper.RepriceQuotationDetail(detail, serviceById, productById);
            cloneRequest.Items.Add(new QuotationCreateItemDto
            {
                SourceQuotationDetailId = detail.Id,
                DiagnosisRecommendationId = detail.DiagnosisRecommendationId,
                ItemType = detail.ItemType,
                ItemId = detail.ItemId,
                ItemName = detail.ItemName,
                Description = detail.Description,
                Quantity = detail.Quantity,
                UnitPriceAtTime = repriced.UnitPrice,
                CostPriceAtTime = repriced.CostPrice,
                DiscountValue = 0m,
                TaxRate = repriced.TaxRate,
                PayerType = detail.PayerType,
                InsuranceCompanyId = detail.InsuranceCompanyId,
                InsuranceCoveredAmount = detail.InsuranceCoveredAmount,
                CustomerCoveredAmount = detail.CustomerCoveredAmount,
                IsBlocking = detail.IsBlocking,
                IsMandatory = detail.IsMandatory,
                EstimatedMinutes = detail.EstimatedMinutes,
                IsReturnOldPart = detail.IsReturnOldPart,
                Status = markCanceled ? QuotationWorkflowConstants.DetailStatusCanceled : (string.IsNullOrWhiteSpace(detail.Status) ? QuotationWorkflowConstants.DetailStatusActive : detail.Status),
                CancelReason = markCanceled
                    ? (QuotationValidationHelper.NormalizeText(request.Note, 500) ?? "Khách yêu cầu hủy hạng mục")
                    : detail.CancelReason,
                CanceledBy = markCanceled ? request.ActorStaffId : detail.CanceledBy
            });
        }

        if (cloneRequest.Items.Count == 0)
        {
            throw new InvalidOperationException("Quotation clone has no remaining items.");
        }

        var cloned = await CreateQuotationAsync(source.ServiceOrderId, cloneRequest, request.ActorStaffId);
        var clonedQuotationId = cloned.Id;

        await _buildStore.ExecuteTransactionalAsync(async () =>
        {
            var clonedEntity = (await _approvalStore.GetQuotationWithDetailsAndDiscountsAsync(clonedQuotationId))!;
            clonedEntity.CustomerDecisionStatus = QuotationWorkflowConstants.ApprovalPending;
            clonedEntity.UpdatedBy = request.ActorStaffId;
            clonedEntity.UpdatedAt = VietnamTime.Now;

            var sourceDiscounts = source.QuotationDiscounts ?? new List<QuotationDiscount>();
            if (sourceDiscounts.Count > 0)
            {
                var copiedDiscounts = sourceDiscounts.Select(discount => new QuotationDiscount
                {
                    QuotationId = clonedEntity.Id,
                    DiscountType = discount.DiscountType,
                    CustomerTierId = discount.CustomerTierId,
                    VoucherId = discount.VoucherId,
                    DiscountValue = discount.DiscountValue,
                    ValueType = discount.ValueType,
                    AppliedAmount = discount.AppliedAmount,
                    Reason = discount.Reason,
                    AppliedBy = discount.AppliedBy,
                    CreatedAt = discount.CreatedAt ?? VietnamTime.Now
                }).ToList();
                _approvalStore.AddQuotationDiscounts(copiedDiscounts);
                clonedEntity.QuotationDiscounts = copiedDiscounts;
                QuotationPricingHelper.RecalculateQuotationTotals(clonedEntity);
                clonedEntity.InsuranceChangeType = await QuotationValidationHelper.ResolveInsuranceChangeTypeAsync(_lineageStore, clonedEntity, null);
            }

            if (removedMandatoryRows.Count > 0)
            {
                var now = VietnamTime.Now;
                var pendingRows = removedMandatoryRows.Select(row => new MandatoryOverrideRequest
                {
                    ServiceOrderId = source.ServiceOrderId,
                    QuotationId = clonedEntity.Id,
                    QuotationDetailId = row.Id,
                    Status = nameof(MandatoryOverrideStatusCode.PENDING_FOREMAN),
                    Reason = string.IsNullOrWhiteSpace(request.Note) ? "Mandatory item removed on quotation clone." : request.Note!.Trim(),
                    RequestedBy = request.ActorStaffId,
                    RequestedAt = now,
                    CreatedAt = now
                }).ToList();

                _approvalStore.AddMandatoryOverrideRequests(pendingRows);
            }

            await _buildStore.SaveChangesInTransactionAsync();
            return 0;
        });

        var warnings = new List<string>();
        if (removedInsuranceAmount > 0)
        {
            warnings.Add("Tổng bồi thường đã thay đổi. Vui lòng cập nhật với Giám định viên.");
        }

        var latest = await _queryService.GetQuotationAsync(clonedQuotationId);
        if (latest != null)
        {
            if (warnings.Count > 0)
            {
                latest.Warnings = warnings;
            }

            return latest;
        }

        var fallbackEntity = (await _approvalStore.GetQuotationWithDetailsAndDiscountsAsync(clonedQuotationId))!;
        return QuotationMappingHelper.MapQuotationResponse(fallbackEntity, warnings);
    }
}
