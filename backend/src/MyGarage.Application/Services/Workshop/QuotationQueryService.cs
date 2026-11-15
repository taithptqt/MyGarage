using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class QuotationQueryService : IQuotationQueryService
{
    private readonly IQuotationApprovalStore _approvalStore;
    private readonly IQuotationBuildStore _buildStore;
    private readonly IQuotationLineageQueryStore _lineageStore;

    public QuotationQueryService(
        IQuotationApprovalStore approvalStore,
        IQuotationBuildStore buildStore,
        IQuotationLineageQueryStore lineageStore)
    {
        _approvalStore = approvalStore;
        _buildStore = buildStore;
        _lineageStore = lineageStore;
    }

    public async Task<QuotationResponseDto?> GetQuotationAsync(long quotationId)
    {
        var quotation = await _approvalStore.GetQuotationWithDetailsAndDiscountsAsync(quotationId);
        if (quotation != null)
        {
            await EnsureQuotationTitleByVersionAsync(quotation);
        }

        if (quotation == null)
        {
            return null;
        }

        var supplementaryMetaByDetailId = await QuotationMappingHelper.BuildSupplementaryMetaByQuotationDetailIdAsync(
            _lineageStore,
            quotation.ServiceOrderId);
        return QuotationMappingHelper.MapQuotationResponse(quotation, null, supplementaryMetaByDetailId);
    }

    public async Task<IReadOnlyList<QuotationDiscountResponseDto>> GetQuotationDiscountsAsync(long quotationId)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        var exists = await _approvalStore.QuotationExistsAsync(quotationId);
        if (!exists)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        var discounts = await _approvalStore.GetQuotationDiscountsAsNoTrackingAsync(quotationId);
        return discounts.Select(QuotationMappingHelper.MapQuotationDiscountResponse).ToList();
    }

    public async Task<IReadOnlyList<QuotationResponseDto>> GetQuotationsByServiceOrderAsync(long serviceOrderId)
    {
        var rows = await _approvalStore.GetQuotationsByServiceOrderWithDetailsAndDiscountsAsync(serviceOrderId);

        var changed = false;
        foreach (var row in rows)
        {
            changed |= QuotationValidationHelper.EnsureQuotationTitleByVersion(row);
        }

        if (changed)
        {
            await _buildStore.SaveChangesInTransactionAsync();
        }

        var supplementaryMetaByDetailId = await QuotationMappingHelper.BuildSupplementaryMetaByQuotationDetailIdAsync(
            _lineageStore,
            serviceOrderId);
        return rows.Select(x => QuotationMappingHelper.MapQuotationResponse(x, null, supplementaryMetaByDetailId)).ToList();
    }

    public async Task<IReadOnlyList<MandatoryOverrideRequestResponseDto>> GetMandatoryOverrideRequestsByServiceOrderAsync(long serviceOrderId)
    {
        if (serviceOrderId <= 0)
        {
            return Array.Empty<MandatoryOverrideRequestResponseDto>();
        }

        var rows = await _approvalStore.GetMandatoryOverrideRequestsByServiceOrderAsync(serviceOrderId);

        return rows.Select(x => new MandatoryOverrideRequestResponseDto
        {
            Id = x.Id,
            ServiceOrderId = x.ServiceOrderId,
            QuotationId = x.QuotationId,
            QuotationDetailId = x.QuotationDetailId,
            Status = x.Status,
            Reason = x.Reason,
            RequestedBy = x.RequestedBy,
            RequestedAt = x.RequestedAt,
            DecidedBy = x.DecidedBy,
            DecidedAt = x.DecidedAt,
            DecisionNote = x.DecisionNote,
            QuotationCode = x.Quotation?.Code,
            QuotationVersionNo = x.Quotation?.VersionNo,
            QuotationDetailItemName = x.QuotationDetail?.ItemName,
            QuotationDetailIsMandatory = x.QuotationDetail?.IsMandatory ?? false
        }).ToList();
    }

    private async Task EnsureQuotationTitleByVersionAsync(Quotation quotation)
    {
        if (QuotationValidationHelper.EnsureQuotationTitleByVersion(quotation))
        {
            await _buildStore.SaveChangesInTransactionAsync();
        }
    }
}
