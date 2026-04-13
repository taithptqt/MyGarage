using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class QuotationWorkflowService : IQuotationWorkflowService
{
    private readonly IGarageContext _context;
    private readonly IServiceOrderDetailService _serviceOrderDetailService;

    private const string StatusDraft = nameof(QuotationStatusCode.DRAFT);
    private const string StatusWaitingApproval = nameof(QuotationStatusCode.WAITING_APPROVAL);
    private const string StatusApproved = nameof(QuotationStatusCode.APPROVED);
    private const string StatusRejected = nameof(QuotationStatusCode.REJECTED);
    private const string StatusSuperseded = nameof(QuotationStatusCode.SUPERSEDED);

    private const string ApprovalPending = nameof(QuotationDetailApprovalStatusCode.PENDING);
    private const string ApprovalApproved = nameof(QuotationDetailApprovalStatusCode.APPROVED);
    private const string ApprovalRejected = nameof(QuotationDetailApprovalStatusCode.REJECTED);

    public QuotationWorkflowService(IGarageContext context, IServiceOrderDetailService serviceOrderDetailService)
    {
        _context = context;
        _serviceOrderDetailService = serviceOrderDetailService;
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

        var serviceOrderExists = await _context.ServiceOrders.AsNoTracking().AnyAsync(x => x.Id == serviceOrderId);
        if (!serviceOrderExists)
        {
            throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
        }

        var nextVersion = await _context.Quotations.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Select(x => (int?)x.VersionNo)
            .MaxAsync() ?? 0;
        nextVersion += 1;

        var now = VietnamTime.Now;
        var normalizedType = NormalizeQuotationType(request.QuotationType);
        var quotation = new Quotation
        {
            Code = BuildQuotationCode(serviceOrderId, nextVersion, now),
            ServiceOrderId = serviceOrderId,
            Title = NormalizeText(request.Title, 255),
            QuotationType = normalizedType,
            VersionNo = nextVersion,
            Status = request.SubmitForApproval ? StatusWaitingApproval : StatusDraft,
            ValidUntil = request.ValidUntil,
            ServiceAdvisorId = request.ServiceAdvisorId,
            CustomerNotes = NormalizeText(request.CustomerNotes, 1000),
            InternalNotes = NormalizeText(request.InternalNotes, 1000),
            CreatedBy = actorStaffId,
            UpdatedBy = actorStaffId,
            CreatedAt = now,
            UpdatedAt = now
        };

        var details = request.Items
            .Where(x => x != null)
            .Select(x => BuildQuotationDetailEntity(x, serviceOrderId, actorStaffId, now))
            .ToList();

        if (details.Count == 0)
        {
            throw new InvalidOperationException("Báo giá không có hạng mục hợp lệ.");
        }

        quotation.QuotationDetails = details;
        RecalculateQuotationTotals(quotation);

        var priorOpenQuotations = await _context.Quotations
            .Where(x =>
                x.ServiceOrderId == serviceOrderId &&
                x.Status != StatusSuperseded &&
                x.Status != StatusApproved)
            .ToListAsync();

        foreach (var row in priorOpenQuotations)
        {
            row.Status = StatusSuperseded;
            row.UpdatedAt = now;
            row.UpdatedBy = actorStaffId;
        }

        _context.Quotations.Add(quotation);
        await _context.SaveChangesAsync();

        return MapQuotationResponse(quotation);
    }

    public async Task<QuotationResponseDto?> GetQuotationAsync(long quotationId)
    {
        var quotation = await _context.Quotations.AsNoTracking()
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        return quotation == null ? null : MapQuotationResponse(quotation);
    }

    public async Task<IReadOnlyList<QuotationResponseDto>> GetQuotationsByServiceOrderAsync(long serviceOrderId)
    {
        var rows = await _context.Quotations.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .OrderByDescending(x => x.VersionNo)
            .ThenByDescending(x => x.CreatedAt ?? DateTime.MinValue)
            .Include(x => x.QuotationDetails)
            .ToListAsync();

        return rows.Select(MapQuotationResponse).ToList();
    }

    public async Task<QuotationResponseDto> ApproveQuotationAsync(long quotationId, QuotationApproveRequestDto request)
    {
        if (request == null || request.ApprovedBy <= 0)
        {
            throw new InvalidOperationException("Thiếu người duyệt báo giá.");
        }

        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.Status == StatusSuperseded)
        {
            throw new InvalidOperationException("Báo giá đã bị thay thế bởi phiên bản mới.");
        }

        if (quotation.Status == StatusApproved && quotation.IsAppliedToServiceOrder)
        {
            return MapQuotationResponse(quotation);
        }

        var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == quotation.ServiceOrderId);
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
        }

        var now = VietnamTime.Now;
        var decisionMap = (request.Decisions ?? new List<QuotationDecisionDto>())
            .Where(x => x != null && x.QuotationDetailId > 0)
            .GroupBy(x => x.QuotationDetailId)
            .ToDictionary(x => x.Key, x => x.First());

        foreach (var detail in quotation.QuotationDetails)
        {
            if (decisionMap.TryGetValue(detail.Id, out var decision))
            {
                if (decision.Approved)
                {
                    detail.ApprovalStatus = ApprovalApproved;
                    detail.RejectReason = null;
                }
                else
                {
                    detail.ApprovalStatus = ApprovalRejected;
                    detail.RejectReason = NormalizeText(decision.RejectReason, 500) ?? "CUSTOMER_REJECTED";
                }

                detail.ApprovedBy = request.ApprovedBy;
                detail.ApprovedAt = now;
                detail.UpdatedBy = request.ApprovedBy;
                detail.UpdatedAt = now;
            }
            else if (decisionMap.Count == 0)
            {
                detail.ApprovalStatus = ApprovalApproved;
                detail.RejectReason = null;
                detail.ApprovedBy = request.ApprovedBy;
                detail.ApprovedAt = now;
                detail.UpdatedBy = request.ApprovedBy;
                detail.UpdatedAt = now;
            }
        }

        var approvedDetails = quotation.QuotationDetails
            .Where(x => string.Equals(x.ApprovalStatus, ApprovalApproved, StringComparison.OrdinalIgnoreCase))
            .ToList();

        quotation.Status = approvedDetails.Count > 0 ? StatusApproved : StatusRejected;
        quotation.ApprovedBy = request.ApprovedBy;
        quotation.ApprovedAt = now;
        quotation.RejectReason = approvedDetails.Count > 0 ? null : "ALL_ITEMS_REJECTED";
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var note = request.Note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? note
                : $"{quotation.InternalNotes.Trim()} | {note}";
        }

        quotation.UpdatedBy = request.ApprovedBy;
        quotation.UpdatedAt = now;
        RecalculateQuotationTotals(quotation);

        if (approvedDetails.Count > 0 && !quotation.IsAppliedToServiceOrder)
        {
            var approvedIds = approvedDetails.Select(x => x.Id).ToList();
            var alreadySyncedIds = await _context.ServiceOrderDetails.AsNoTracking()
                .Where(x => x.QuotationDetailId.HasValue && approvedIds.Contains(x.QuotationDetailId.Value))
                .Select(x => x.QuotationDetailId!.Value)
                .ToListAsync();
            var syncedIdSet = alreadySyncedIds.ToHashSet();

            var rowsToCreate = approvedDetails
                .Where(x => !syncedIdSet.Contains(x.Id))
                .Select(x => BuildServiceOrderDetailFromQuotation(x, serviceOrder.Id, request.ApprovedBy, now))
                .ToList();
            if (rowsToCreate.Count > 0)
            {
                _context.ServiceOrderDetails.AddRange(rowsToCreate);
            }

            quotation.IsAppliedToServiceOrder = true;
            quotation.AppliedBy = request.ApprovedBy;
            quotation.AppliedAt = now;

            if (serviceOrder.CustomerApprovedAt == null)
            {
                serviceOrder.CustomerApprovedAt = now;
            }

            if (string.Equals(serviceOrder.Status, nameof(ServiceOrderLifecycleStatus.QUOTING), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(serviceOrder.Status, nameof(ServiceOrderLifecycleStatus.WAITING_APPROVAL), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(serviceOrder.Status, nameof(ServiceOrderLifecycleStatus.APPROVED), StringComparison.OrdinalIgnoreCase))
            {
                serviceOrder.Status = nameof(ServiceOrderLifecycleStatus.IN_PROGRESS);
            }

            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = request.ApprovedBy;
        }

        await _context.SaveChangesAsync();

        if (quotation.IsAppliedToServiceOrder)
        {
            await RecalculateServiceOrderTotalsAsync(serviceOrder.Id, request.ApprovedBy, now);
            await _serviceOrderDetailService.SyncServiceOrderWaitingPartsStatusAsync(serviceOrder.Id, request.ApprovedBy);
            await _context.SaveChangesAsync();
        }

        return MapQuotationResponse(quotation);
    }

    public async Task<QuotationResponseDto> SubmitQuotationAsync(long quotationId, long actorStaffId, string? note = null)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        if (actorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự gửi báo giá.");
        }

        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.Status == StatusSuperseded)
        {
            throw new InvalidOperationException("Báo giá đã bị thay thế.");
        }

        if (quotation.Status == StatusApproved || quotation.Status == StatusRejected)
        {
            return MapQuotationResponse(quotation);
        }

        var now = VietnamTime.Now;
        quotation.Status = StatusWaitingApproval;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = actorStaffId;

        if (!string.IsNullOrWhiteSpace(note))
        {
            var cleaned = note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleaned
                : $"{quotation.InternalNotes.Trim()} | {cleaned}";
        }

        await _context.SaveChangesAsync();
        return MapQuotationResponse(quotation);
    }

    public async Task<QuotationResponseDto> SupersedeQuotationAsync(long quotationId, long actorStaffId, string? note = null)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        if (actorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự thao tác.");
        }

        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.IsAppliedToServiceOrder || quotation.Status == StatusApproved)
        {
            throw new InvalidOperationException("Không thể thay thế báo giá đã áp vào lệnh sửa chữa.");
        }

        if (quotation.Status == StatusSuperseded)
        {
            return MapQuotationResponse(quotation);
        }

        var now = VietnamTime.Now;
        quotation.Status = StatusSuperseded;
        quotation.UpdatedBy = actorStaffId;
        quotation.UpdatedAt = now;
        if (!string.IsNullOrWhiteSpace(note))
        {
            var cleaned = note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleaned
                : $"{quotation.InternalNotes.Trim()} | {cleaned}";
        }

        await _context.SaveChangesAsync();
        return MapQuotationResponse(quotation);
    }

    private static string BuildQuotationCode(long serviceOrderId, int versionNo, DateTime now)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"QT-{now:yyyyMMddHHmmss}-{serviceOrderId}-{versionNo:D3}");
    }

    private static string NormalizeQuotationType(string? rawValue)
    {
        var normalized = (rawValue ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized == nameof(QuotationTypeCode.INSURANCE) || normalized == nameof(QuotationTypeCode.MIXED))
        {
            return normalized;
        }

        return nameof(QuotationTypeCode.NORMAL);
    }

    private static string? NormalizeText(string? value, int maxLen)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLen ? normalized : normalized[..maxLen];
    }

    private static QuotationDetail BuildQuotationDetailEntity(QuotationCreateItemDto dto, long serviceOrderId, long? actorStaffId, DateTime now)
    {
        var quantity = dto.Quantity <= 0 ? 1m : dto.Quantity;
        var unitPrice = dto.UnitPriceAtTime < 0 ? 0 : dto.UnitPriceAtTime;
        var discount = dto.DiscountValue < 0 ? 0 : dto.DiscountValue;
        var taxRate = dto.TaxRate < 0 ? 0 : dto.TaxRate;
        var subTotal = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        var taxableBase = Math.Max(0, subTotal - discount);
        var taxAmount = Math.Round(taxableBase * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
        var finalAmount = Math.Round(Math.Max(0, taxableBase + taxAmount), 2, MidpointRounding.AwayFromZero);

        return new QuotationDetail
        {
            ServiceOrderId = serviceOrderId,
            DiagnoseRecommendationId = dto.DiagnoseRecommendationId,
            ItemType = (dto.ItemType ?? string.Empty).Trim().ToUpperInvariant(),
            ItemId = dto.ItemId,
            ItemName = (dto.ItemName ?? string.Empty).Trim(),
            Description = NormalizeText(dto.Description, 1000),
            Quantity = quantity,
            UnitPriceAtTime = unitPrice,
            CostPriceAtTime = dto.CostPriceAtTime,
            SubTotal = subTotal,
            DiscountValue = discount,
            TaxRate = taxRate,
            TaxAmount = taxAmount,
            FinalAmount = finalAmount,
            PayerType = string.IsNullOrWhiteSpace(dto.PayerType) ? "CUSTOMER" : dto.PayerType.Trim().ToUpperInvariant(),
            InsuranceCompanyId = dto.InsuranceCompanyId,
            InsuranceRequestedAmount = dto.InsuranceRequestedAmount,
            CustomerCoPayAmount = dto.CustomerCoPayAmount,
            ApprovalStatus = ApprovalPending,
            IsBlocking = dto.IsBlocking,
            EstimatedMinutes = dto.EstimatedMinutes,
            IsReturnOldPart = dto.IsReturnOldPart,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorStaffId,
            UpdatedBy = actorStaffId
        };
    }

    private static ServiceOrderDetail BuildServiceOrderDetailFromQuotation(QuotationDetail detail, long serviceOrderId, long actorStaffId, DateTime now)
    {
        return new ServiceOrderDetail
        {
            ServiceOrderId = serviceOrderId,
            QuotationDetailId = detail.Id,
            DiagnoseRecommendationId = detail.DiagnoseRecommendationId,
            ItemType = detail.ItemType,
            ItemId = detail.ItemId,
            ItemName = detail.ItemName,
            Description = detail.Description,
            Quantity = detail.Quantity <= 0 ? 1m : detail.Quantity,
            EstimatedMinutes = detail.EstimatedMinutes,
            CostPriceAtTime = detail.CostPriceAtTime,
            UnitPriceAtTime = detail.UnitPriceAtTime,
            Unit = "EA",
            SubTotal = detail.SubTotal,
            DiscountValue = detail.DiscountValue,
            TaxRate = detail.TaxRate,
            TaxAmount = detail.TaxAmount,
            FinalAmount = detail.FinalAmount,
            PayerType = detail.PayerType,
            InsuranceCompanyId = detail.InsuranceCompanyId,
            InsuranceRequestedAmount = detail.InsuranceRequestedAmount,
            CustomerCoPayAmount = detail.CustomerCoPayAmount,
            Status = nameof(ServiceOrderDetailStatusCode.APPROVED),
            IsReturnOldPart = detail.IsReturnOldPart,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorStaffId,
            UpdatedBy = actorStaffId
        };
    }

    private static void RecalculateQuotationTotals(Quotation quotation)
    {
        var details = quotation.QuotationDetails ?? new List<QuotationDetail>();
        quotation.SubTotalAmount = details.Sum(x => x.SubTotal);
        quotation.TotalDiscount = details.Sum(x => x.DiscountValue);
        quotation.TaxAmount = details.Sum(x => x.TaxAmount);
        quotation.FinalAmount = details.Sum(x => x.FinalAmount);
        quotation.TotalInsuranceAmount = details.Sum(x =>
            x.InsuranceRequestedAmount
            ?? (string.Equals(x.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase) ? x.FinalAmount : 0m));
        quotation.TotalCustomerAmount = details.Sum(x =>
            x.CustomerCoPayAmount
            ?? (!string.Equals(x.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase)
                ? x.FinalAmount
                : Math.Max(0m, x.FinalAmount - (x.InsuranceRequestedAmount ?? 0m))));
    }

    private async Task RecalculateServiceOrderTotalsAsync(long serviceOrderId, long actorStaffId, DateTime now)
    {
        var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
        if (serviceOrder == null)
        {
            return;
        }

        var summary = await _context.ServiceOrderDetails.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                SubTotal = group.Sum(x => x.SubTotal),
                Discount = group.Sum(x => x.DiscountValue ?? 0m),
                Tax = group.Sum(x => x.TaxAmount),
                Final = group.Sum(x => x.FinalAmount)
            })
            .FirstOrDefaultAsync();

        serviceOrder.SubTotalAmount = summary?.SubTotal ?? 0m;
        serviceOrder.TotalDiscount = summary?.Discount ?? 0m;
        serviceOrder.TaxAmount = summary?.Tax ?? 0m;
        serviceOrder.FinalAmount = summary?.Final ?? 0m;
        serviceOrder.UpdatedBy = actorStaffId;
        serviceOrder.UpdatedAt = now;
    }

    private static QuotationResponseDto MapQuotationResponse(Quotation quotation)
    {
        return new QuotationResponseDto
        {
            Id = quotation.Id,
            Code = quotation.Code,
            ServiceOrderId = quotation.ServiceOrderId,
            Title = quotation.Title,
            QuotationType = quotation.QuotationType,
            VersionNo = quotation.VersionNo,
            Status = quotation.Status,
            ValidUntil = quotation.ValidUntil,
            SubTotalAmount = quotation.SubTotalAmount,
            TotalDiscount = quotation.TotalDiscount,
            TaxAmount = quotation.TaxAmount,
            FinalAmount = quotation.FinalAmount,
            TotalInsuranceAmount = quotation.TotalInsuranceAmount,
            TotalCustomerAmount = quotation.TotalCustomerAmount,
            IsAppliedToServiceOrder = quotation.IsAppliedToServiceOrder,
            AppliedAt = quotation.AppliedAt,
            Details = (quotation.QuotationDetails ?? new List<QuotationDetail>())
                .OrderBy(x => x.Id)
                .Select(x => new QuotationDetailResponseDto
                {
                    Id = x.Id,
                    DiagnoseRecommendationId = x.DiagnoseRecommendationId,
                    ItemType = x.ItemType,
                    ItemId = x.ItemId,
                    ItemName = x.ItemName,
                    Description = x.Description,
                    Quantity = x.Quantity,
                    UnitPriceAtTime = x.UnitPriceAtTime,
                    CostPriceAtTime = x.CostPriceAtTime,
                    SubTotal = x.SubTotal,
                    DiscountValue = x.DiscountValue,
                    TaxRate = x.TaxRate,
                    TaxAmount = x.TaxAmount,
                    FinalAmount = x.FinalAmount,
                    PayerType = x.PayerType,
                    InsuranceCompanyId = x.InsuranceCompanyId,
                    InsuranceRequestedAmount = x.InsuranceRequestedAmount,
                    CustomerCoPayAmount = x.CustomerCoPayAmount,
                    ApprovalStatus = x.ApprovalStatus,
                    RejectReason = x.RejectReason,
                    IsBlocking = x.IsBlocking,
                    EstimatedMinutes = x.EstimatedMinutes,
                    IsReturnOldPart = x.IsReturnOldPart
                })
                .ToList()
        };
    }
}
