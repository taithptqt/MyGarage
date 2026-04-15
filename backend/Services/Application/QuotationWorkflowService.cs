using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    private const string StatusDraft = nameof(QuotationStatusCode.DRAFT);
    private const string StatusWaitingApproval = nameof(QuotationStatusCode.WAITING_APPROVAL);
    private const string StatusSentToCustomer = nameof(QuotationStatusCode.SENT_TO_CUSTOMER);
    private const string StatusPendingInsurance = nameof(QuotationStatusCode.PENDING_INSURANCE);
    private const string StatusPendingCustomer = nameof(QuotationStatusCode.PENDING_CUSTOMER);
    private const string StatusApproved = nameof(QuotationStatusCode.APPROVED);
    private const string StatusCustomerApproved = nameof(QuotationStatusCode.CUSTOMER_APPROVED);
    private const string StatusCustomerRejected = nameof(QuotationStatusCode.CUSTOMER_REJECTED);
    private const string StatusRejected = nameof(QuotationStatusCode.REJECTED);
    private const string StatusSuperseded = nameof(QuotationStatusCode.SUPERSEDED);
    private const string StatusCanceled = nameof(QuotationStatusCode.CANCELED);

    private const string ApprovalPending = nameof(QuotationDetailApprovalStatusCode.PENDING);
    private const string ApprovalApproved = nameof(QuotationDetailApprovalStatusCode.APPROVED);
    private const string ApprovalRejected = nameof(QuotationDetailApprovalStatusCode.REJECTED);
    private const string LinkStatusActive = "ACTIVE";
    private const string LinkStatusExpired = "EXPIRED";
    private const string LinkStatusRevoked = "REVOKED";
    private const string LinkStatusUsed = "USED";
    private const int DefaultPortalExpiryDays = 15;

    public QuotationWorkflowService(
        IGarageContext context,
        IServiceOrderDetailService serviceOrderDetailService,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _context = context;
        _serviceOrderDetailService = serviceOrderDetailService;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task<QuotationResponseDto> CreateQuotationAsync(long serviceOrderId, QuotationCreateRequestDto request, long? actorStaffId = null)
    {
        if (serviceOrderId <= 0)
        {
            throw new InvalidOperationException("Service order kh\u00F4ng h\u1EE3p l\u1EC7.");
        }

        if (request == null || request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("B\u00E1o gi\u00E1 ph\u1EA3i c\u00F3 \u00EDt nh\u1EA5t 1 h\u1EA1ng m\u1EE5c.");
        }

        var serviceOrderExists = await _context.ServiceOrders.AsNoTracking().AnyAsync(x => x.Id == serviceOrderId);
        if (!serviceOrderExists)
        {
            throw new InvalidOperationException("Kh\u00F4ng t\u00ECm th\u1EA5y l\u1EC7nh s\u1EEDa ch\u1EEFa.");
        }

        if (request.DiagnosisId.HasValue && request.DiagnosisId.Value > 0)
        {
            var diagnosisBelongsToServiceOrder = await _context.Diagnoses.AsNoTracking()
                .AnyAsync(x => x.Id == request.DiagnosisId.Value && x.ServiceOrderId == serviceOrderId);
            if (!diagnosisBelongsToServiceOrder)
            {
                throw new InvalidOperationException("DiagnosisId không thuộc lệnh sửa chữa hiện tại.");
            }
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
            DiagnosisId = request.DiagnosisId,
            Title = BuildQuotationTitle(nextVersion),
            QuotationType = normalizedType,
            VersionNo = nextVersion,
            Status = request.SubmitForApproval ? StatusWaitingApproval : StatusDraft,
            InsuranceClaimNumber = NormalizeText(request.InsuranceClaimNumber, 100),
            ValidUntil = request.ValidUntil,
            ServiceAdvisorId = request.ServiceAdvisorId,
            CustomerNotes = NormalizeText(request.CustomerNotes, 1000),
            InternalNotes = NormalizeText(request.InternalNotes, 1000),
            TeamLeaderApprovalStatus = ApprovalPending,
            FinalApprovalStatus = ApprovalPending,
            CustomerDecisionStatus = ApprovalPending,
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
            throw new InvalidOperationException("B\u00E1o gi\u00E1 kh\u00F4ng c\u00F3 h\u1EA1ng m\u1EE5c h\u1EE3p l\u1EC7.");
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

        var serviceOrder = await _context.ServiceOrders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == serviceOrderId);
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Service order not found.");
        }

        var diagnosisIdFilter = (diagnosisIds ?? Array.Empty<long>()).Where(x => x > 0).Distinct().ToList();

        var diagnosesQuery = _context.Diagnoses.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Where(x => x.Status != nameof(DiagnosisStatus.CANCELED));
        if (diagnosisIdFilter.Count > 0)
        {
            diagnosesQuery = diagnosesQuery.Where(x => diagnosisIdFilter.Contains(x.Id));
        }

        var diagnoses = await diagnosesQuery
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
            .ToListAsync();
        if (diagnoses.Count == 0)
        {
            throw new InvalidOperationException("No diagnosis found to create quotation.");
        }

        var sourceDiagnosisIds = diagnoses.Select(x => x.Id).Distinct().ToList();
        var latestAssignmentIdsByDiagnosis = await _context.DiagnosisAssignments.AsNoTracking()
            .Where(x => sourceDiagnosisIds.Contains(x.DiagnosisId))
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        var latestAssignmentIds = latestAssignmentIdsByDiagnosis
            .GroupBy(x => x.DiagnosisId)
            .Select(x => x.First().Id)
            .ToList();

        var recommendations = await _context.DiagnoseRecommendations.AsNoTracking()
            .Where(x =>
                (x.DiagnosisAssignmentId.HasValue && latestAssignmentIds.Contains(x.DiagnosisAssignmentId.Value)) ||
                (!x.DiagnosisAssignmentId.HasValue && sourceDiagnosisIds.Contains(x.DiagnosisId)))
            .Where(x => x.RecommendationStatus != nameof(RecommendationLifecycleStatusCode.SOFT_DELETED_BY_TL))
            .ToListAsync();
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

        var serviceById = await _context.StandardServices.AsNoTracking()
            .Where(x => standardServiceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x);
        var productById = await _context.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x);

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var nextSnapshotVersion = await _context.TechnicalProposalSnapshots.AsNoTracking()
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Select(x => (int?)x.VersionNo)
                .MaxAsync() ?? 0;
            nextSnapshotVersion += 1;

            var latestDiagnosis = diagnoses.First();
            var now = VietnamTime.Now;
            var snapshot = new TechnicalProposalSnapshot
            {
                ServiceOrderId = serviceOrderId,
                DiagnosisId = latestDiagnosis.Id,
                VersionNo = nextSnapshotVersion,
                Status = "LOCKED",
                LockedBy = actorStaffId,
                LockedAt = now,
                CreatedBy = actorStaffId,
                CreatedAt = now
            };
            _context.TechnicalProposalSnapshots.Add(snapshot);
            await _context.SaveChangesAsync();

            var request = new QuotationCreateRequestDto
            {
                DiagnosisId = latestDiagnosis.Id,
                Title = BuildQuotationTitle(nextSnapshotVersion),
                QuotationType = nameof(QuotationTypeCode.INSURANCE),
                InternalNotes = note,
                SubmitForApproval = false
            };

            var snapshotDetails = new List<TechnicalProposalSnapshotDetail>();
            foreach (var rec in recommendations.OrderBy(x => x.Id))
            {
                var mapped = MapRecommendationToPrice(rec, serviceById, productById);
                snapshotDetails.Add(new TechnicalProposalSnapshotDetail
                {
                    SnapshotId = snapshot.Id,
                    DiagnosisRecommendationId = rec.Id,
                    ItemType = mapped.ItemType,
                    ItemId = mapped.ItemId,
                    ItemName = mapped.ItemName,
                    Quantity = rec.Quantity <= 0 ? 1 : rec.Quantity,
                    IsMandatory = IsMandatoryByUrgency(rec.Urgency),
                    TechnicianNotes = rec.TechnicianNotes,
                    CreatedAt = now
                });

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
                    IsMandatory = IsMandatoryByUrgency(rec.Urgency),
                    EstimatedMinutes = null,
                    IsReturnOldPart = false
                });
            }

            _context.TechnicalProposalSnapshotDetails.AddRange(snapshotDetails);
            await _context.SaveChangesAsync();

            var quotation = await CreateQuotationAsync(serviceOrderId, request, actorStaffId);
            var quotationEntity = await _context.Quotations.FirstAsync(x => x.Id == quotation.Id);
            quotationEntity.SourceSnapshotId = snapshot.Id;
            quotationEntity.Status = StatusPendingInsurance;
            quotationEntity.TeamLeaderApprovalStatus = ApprovalApproved;
            quotationEntity.TechnicalApprovedBy = actorStaffId;
            quotationEntity.TechnicalApprovedAt = now;
            quotationEntity.FinalApprovalStatus = ApprovalApproved;
            quotationEntity.FinalApprovedBy = actorStaffId;
            quotationEntity.FinalApprovedAt = now;
            quotationEntity.CustomerDecisionStatus = ApprovalPending;
            quotationEntity.UpdatedBy = actorStaffId;
            quotationEntity.UpdatedAt = now;
            await _context.SaveChangesAsync();

            await tx.CommitAsync();
            return await GetQuotationAsync(quotationEntity.Id) ?? MapQuotationResponse(quotationEntity);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<QuotationResponseDto?> GetQuotationAsync(long quotationId)
    {
        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation != null)
        {
            await EnsureQuotationTitleByVersionAsync(quotation);
        }
        return quotation == null ? null : MapQuotationResponse(quotation);
    }

    public async Task<IReadOnlyList<QuotationResponseDto>> GetQuotationsByServiceOrderAsync(long serviceOrderId)
    {
        var rows = await _context.Quotations
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .OrderByDescending(x => x.VersionNo)
            .ThenByDescending(x => x.CreatedAt ?? DateTime.MinValue)
            .Include(x => x.QuotationDetails)
            .ToListAsync();

        var changed = false;
        foreach (var row in rows)
        {
            changed |= EnsureQuotationTitleByVersion(row);
        }
        if (changed)
        {
            await _context.SaveChangesAsync();
        }

        return rows.Select(x => MapQuotationResponse(x)).ToList();
    }

    public async Task<IReadOnlyList<MandatoryOverrideRequestResponseDto>> GetMandatoryOverrideRequestsByServiceOrderAsync(long serviceOrderId)
    {
        if (serviceOrderId <= 0)
        {
            return Array.Empty<MandatoryOverrideRequestResponseDto>();
        }

        var rows = await _context.MandatoryOverrideRequests.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .OrderByDescending(x => x.RequestedAt)
            .Include(x => x.Quotation)
            .Include(x => x.QuotationDetail)
            .ToListAsync();

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

    public async Task<QuotationResponseDto> CloneQuotationAsync(long quotationId, QuotationCloneRequestDto request)
    {
        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Actor staff is required.");
        }

        var source = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (source == null)
        {
            throw new InvalidOperationException("Quotation not found.");
        }

        if (string.Equals(source.Status, StatusCanceled, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot clone a cancelled quotation.");
        }

        var removedIds = (request.RemoveQuotationDetailIds ?? new List<long>())
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();

        var serviceIds = source.QuotationDetails
            .Where(x => x.ItemId.HasValue && string.Equals(NormalizeServiceOrderDetailItemType(x.ItemType), "LABOR", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ItemId!.Value)
            .Distinct()
            .ToList();
        var productIds = source.QuotationDetails
            .Where(x => x.ItemId.HasValue && string.Equals(NormalizeServiceOrderDetailItemType(x.ItemType), "PART", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ItemId!.Value)
            .Distinct()
            .ToList();

        var serviceById = await _context.StandardServices.AsNoTracking()
            .Where(x => serviceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x);
        var productById = await _context.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x);

        var cloneRequest = new QuotationCreateRequestDto
        {
            DiagnosisId = source.DiagnosisId,
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
            if (removedIds.Contains(detail.Id))
            {
                if (detail.IsMandatory)
                {
                    removedMandatoryRows.Add(detail);
                }

                if (string.Equals(detail.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(detail.PayerType, "SPLIT", StringComparison.OrdinalIgnoreCase))
                {
                    removedInsuranceAmount += detail.InsuranceCoveredAmount
                        ?? detail.InsuranceRequestedAmount
                        ?? (string.Equals(detail.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase) ? detail.FinalAmount : 0m);
                }

                continue;
            }

            var repriced = RepriceQuotationDetail(detail, serviceById, productById);
            cloneRequest.Items.Add(new QuotationCreateItemDto
            {
                DiagnosisRecommendationId = detail.DiagnosisRecommendationId,
                ItemType = detail.ItemType,
                ItemId = detail.ItemId,
                ItemName = detail.ItemName,
                Description = detail.Description,
                Quantity = detail.Quantity,
                UnitPriceAtTime = repriced.UnitPrice,
                CostPriceAtTime = repriced.CostPrice,
                DiscountValue = detail.DiscountValue,
                TaxRate = repriced.TaxRate,
                PayerType = detail.PayerType,
                InsuranceCompanyId = detail.InsuranceCompanyId,
                InsuranceCoveredAmount = detail.InsuranceCoveredAmount,
                CustomerCoveredAmount = detail.CustomerCoveredAmount,
                InsuranceRequestedAmount = detail.InsuranceRequestedAmount,
                CustomerCoPayAmount = detail.CustomerCoPayAmount,
                IsBlocking = detail.IsBlocking,
                IsMandatory = detail.IsMandatory,
                EstimatedMinutes = detail.EstimatedMinutes,
                IsReturnOldPart = detail.IsReturnOldPart
            });
        }

        if (cloneRequest.Items.Count == 0)
        {
            throw new InvalidOperationException("Quotation clone has no remaining items.");
        }

        var cloned = await CreateQuotationAsync(source.ServiceOrderId, cloneRequest, request.ActorStaffId);
        var clonedEntity = await _context.Quotations.FirstAsync(x => x.Id == cloned.Id);
        clonedEntity.SourceSnapshotId = source.SourceSnapshotId;
        clonedEntity.TeamLeaderApprovalStatus = source.TeamLeaderApprovalStatus;
        clonedEntity.TechnicalApprovedBy = source.TechnicalApprovedBy;
        clonedEntity.TechnicalApprovedAt = source.TechnicalApprovedAt;
        clonedEntity.FinalApprovalStatus = source.FinalApprovalStatus;
        clonedEntity.FinalApprovedBy = source.FinalApprovedBy;
        clonedEntity.FinalApprovedAt = source.FinalApprovedAt;
        clonedEntity.CustomerDecisionStatus = ApprovalPending;
        clonedEntity.UpdatedBy = request.ActorStaffId;
        clonedEntity.UpdatedAt = VietnamTime.Now;
        await _context.SaveChangesAsync();

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

            _context.MandatoryOverrideRequests.AddRange(pendingRows);
            await _context.SaveChangesAsync();
        }

        var warnings = new List<string>();
        if (removedInsuranceAmount > 0)
        {
            warnings.Add("Tổng bồi thường đã thay đổi. Vui lòng cập nhật với Giám định viên");
        }

        var latest = await GetQuotationAsync(clonedEntity.Id);
        if (latest != null)
        {
            if (warnings.Count > 0)
            {
                latest.Warnings = warnings;
            }

            return latest;
        }

        return MapQuotationResponse(clonedEntity, warnings);
    }

    public async Task<QuotationResponseDto> UpdateSplitBillingAsync(long quotationId, QuotationSplitBillingUpdateRequestDto request)
    {
        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự cập nhật tách bill.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("Thiếu dòng báo giá cần cập nhật tách bill.");
        }

        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (string.Equals(quotation.Status, StatusSuperseded, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, StatusCanceled, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, StatusCustomerApproved, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Không thể cập nhật tách bill cho báo giá đã khóa.");
        }

        var now = VietnamTime.Now;
        var updatesByDetailId = request.Items
            .Where(x => x != null && x.QuotationDetailId > 0)
            .GroupBy(x => x.QuotationDetailId)
            .ToDictionary(x => x.Key, x => x.First());

        foreach (var detail in quotation.QuotationDetails)
        {
            if (!updatesByDetailId.TryGetValue(detail.Id, out var update))
            {
                continue;
            }

            var split = CalculateSplitBilling(
                detail.FinalAmount,
                update.InsuranceCoveredAmount,
                null,
                update.PayerType ?? detail.PayerType);

            detail.PayerType = split.PayerType;
            detail.InsuranceCoveredAmount = split.InsuranceCoveredAmount;
            detail.CustomerCoveredAmount = split.CustomerCoveredAmount;
            detail.InsuranceRequestedAmount = split.InsuranceCoveredAmount;
            detail.CustomerCoPayAmount = split.CustomerCoveredAmount;
            detail.UpdatedAt = now;
            detail.UpdatedBy = request.ActorStaffId;
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var cleaned = request.Note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleaned
                : $"{quotation.InternalNotes.Trim()} | {cleaned}";
        }

        quotation.Status = StatusPendingCustomer;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = request.ActorStaffId;
        RecalculateQuotationTotals(quotation);

        await _context.SaveChangesAsync();
        return MapQuotationResponse(quotation);
    }

    public async Task DecideMandatoryOverrideAsync(long requestId, MandatoryOverrideDecisionRequestDto request)
    {
        if (requestId <= 0 || request == null || request.DecidedBy <= 0)
        {
            throw new InvalidOperationException("Invalid mandatory override decision.");
        }

        var row = await _context.MandatoryOverrideRequests.FirstOrDefaultAsync(x => x.Id == requestId);
        if (row == null)
        {
            throw new InvalidOperationException("Mandatory override request not found.");
        }

        if (!string.Equals(row.Status, nameof(MandatoryOverrideStatusCode.PENDING_FOREMAN), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        row.Status = request.Approve
            ? nameof(MandatoryOverrideStatusCode.APPROVED)
            : nameof(MandatoryOverrideStatusCode.REJECTED);
        row.DecidedBy = request.DecidedBy;
        row.DecidedAt = VietnamTime.Now;
        row.DecisionNote = NormalizeText(request.Note, 1000);
        await _context.SaveChangesAsync();
    }

    public async Task<QuotationResponseDto> ApproveQuotationAsync(long quotationId, QuotationApproveRequestDto request)
    {
        if (request == null || request.ApprovedBy <= 0)
        {
            throw new InvalidOperationException("Thi\u1EBFu ng\u01B0\u1EDDi duy\u1EC7t b\u00E1o gi\u00E1.");
        }

        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Kh\u00F4ng t\u00ECm th\u1EA5y b\u00E1o gi\u00E1.");
        }

        if (quotation.Status == StatusSuperseded)
        {
            throw new InvalidOperationException("B\u00E1o gi\u00E1 \u0111\u00E3 b\u1ECB thay th\u1EBF b\u1EDFi phi\u00EAn b\u1EA3n m\u1EDBi.");
        }

        if (quotation.Status == StatusApproved && quotation.IsAppliedToServiceOrder)
        {
            return MapQuotationResponse(quotation);
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
        quotation.FinalApprovedBy = request.ApprovedBy;
        quotation.FinalApprovedAt = now;
        quotation.FinalApprovalStatus = approvedDetails.Count > 0 ? ApprovalApproved : ApprovalRejected;
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

        await _context.SaveChangesAsync();
        return MapQuotationResponse(quotation);
    }

    public async Task<QuotationResponseDto> ApplyCustomerDecisionAsync(long quotationId, QuotationCustomerDecisionRequestDto request)
    {
        if (request == null)
        {
            throw new InvalidOperationException("Thi\u1EBFu quy\u1EBFt \u0111\u1ECBnh c\u1EE7a kh\u00E1ch h\u00E0ng.");
        }

        var actorId = request.ActorStaffId
            ?? request.CustomerId
            ?? 0;
        if (actorId <= 0)
        {
            throw new InvalidOperationException("Thi\u1EBFu ng\u01B0\u1EDDi x\u00E1c nh\u1EADn quy\u1EBFt \u0111\u1ECBnh c\u1EE7a kh\u00E1ch.");
        }

        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Kh\u00F4ng t\u00ECm th\u1EA5y b\u00E1o gi\u00E1.");
        }

        if (quotation.Status == StatusSuperseded)
        {
            throw new InvalidOperationException("B\u00E1o gi\u00E1 \u0111\u00E3 b\u1ECB thay th\u1EBF.");
        }

        var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == quotation.ServiceOrderId);
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Kh\u00F4ng t\u00ECm th\u1EA5y l\u1EC7nh s\u1EEDa ch\u1EEFa.");
        }

        var now = VietnamTime.Now;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var cleanedNote = request.Note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleanedNote
                : $"{quotation.InternalNotes.Trim()} | {cleanedNote}";
        }

        if (request.Approved)
        {
            if (!string.Equals(quotation.Status, StatusApproved, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(quotation.Status, StatusSentToCustomer, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(quotation.Status, StatusPendingCustomer, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("B\u00E1o gi\u00E1 ch\u01B0a \u0111\u01B0\u1EE3c qu\u1EA3n \u0111\u1ED1c duy\u1EC7t cu\u1ED1i.");
            }

            var hasPendingMandatoryOverride = await _context.MandatoryOverrideRequests.AsNoTracking()
                .AnyAsync(x =>
                    x.QuotationId == quotationId &&
                    x.Status != nameof(MandatoryOverrideStatusCode.APPROVED));
            if (hasPendingMandatoryOverride)
            {
                throw new InvalidOperationException("Mandatory override request is pending or rejected.");
            }

            quotation.Status = StatusCustomerApproved;
            quotation.CustomerDecisionStatus = ApprovalApproved;
            quotation.CustomerDecisionBy = actorId;
            quotation.CustomerDecisionAt = now;
            quotation.UpdatedAt = now;
            quotation.UpdatedBy = actorId;
            await ApplyQuotationToServiceOrderAsync(quotation, serviceOrder, actorId, now);
            return MapQuotationResponse(quotation);
        }

        if (quotation.IsAppliedToServiceOrder)
        {
            throw new InvalidOperationException("Kh\u00F4ng th\u1EC3 t\u1EEB ch\u1ED1i b\u00E1o gi\u00E1 \u0111\u00E3 \u0111\u01B0\u1EE3c \u00E1p v\u00E0o l\u1EC7nh s\u1EEDa ch\u1EEFa.");
        }

        quotation.Status = StatusCustomerRejected;
        quotation.RejectReason = "CUSTOMER_REJECTED";
        quotation.CustomerDecisionStatus = ApprovalRejected;
        quotation.CustomerDecisionBy = actorId;
        quotation.CustomerDecisionAt = now;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = actorId;
        serviceOrder.Status = nameof(ServiceOrderLifecycleStatus.DIAGNOSING);
        serviceOrder.UpdatedAt = now;
        serviceOrder.UpdatedBy = actorId;

        await _context.SaveChangesAsync();
        return MapQuotationResponse(quotation);
    }

    private async Task ApplyQuotationToServiceOrderAsync(Quotation quotation, ServiceOrder serviceOrder, long actorId, DateTime now)
    {
        if (quotation.IsAppliedToServiceOrder)
        {
            return;
        }

        var approvedDetails = quotation.QuotationDetails
            .Where(x => string.Equals(x.ApprovalStatus, ApprovalApproved, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (approvedDetails.Count == 0)
        {
            throw new InvalidOperationException("Kh\u00F4ng c\u00F3 h\u1EA1ng m\u1EE5c \u0111\u01B0\u1EE3c duy\u1EC7t \u0111\u1EC3 \u00E1p v\u00E0o l\u1EC7nh s\u1EEDa ch\u1EEFa.");
        }

        var approvedIds = approvedDetails.Select(x => x.Id).ToList();
        var alreadySyncedIds = await _context.ServiceOrderDetails.AsNoTracking()
            .Where(x => x.QuotationDetailId.HasValue && approvedIds.Contains(x.QuotationDetailId.Value))
            .Select(x => x.QuotationDetailId!.Value)
            .ToListAsync();
        var syncedIdSet = alreadySyncedIds.ToHashSet();

        var rowsToCreate = approvedDetails
            .Where(x => !syncedIdSet.Contains(x.Id))
            .Select(x => BuildServiceOrderDetailFromQuotation(x, serviceOrder.Id, actorId, now))
            .ToList();
        if (rowsToCreate.Count > 0)
        {
            _context.ServiceOrderDetails.AddRange(rowsToCreate);
        }

        quotation.IsAppliedToServiceOrder = true;
        quotation.AppliedBy = actorId;
        quotation.AppliedAt = now;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = actorId;

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
        serviceOrder.UpdatedBy = actorId;

        await _context.SaveChangesAsync();
        await RecalculateServiceOrderTotalsAsync(serviceOrder.Id, actorId, now);
        await _serviceOrderDetailService.SyncServiceOrderWaitingPartsStatusAsync(serviceOrder.Id, actorId);
        await _context.SaveChangesAsync();
    }

    public async Task<QuotationResponseDto> SubmitQuotationAsync(long quotationId, long actorStaffId, string? note = null)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("M\u00E3 b\u00E1o gi\u00E1 kh\u00F4ng h\u1EE3p l\u1EC7.");
        }

        if (actorStaffId <= 0)
        {
            throw new InvalidOperationException("Thi\u1EBFu nh\u00E2n s\u1EF1 g\u1EEDi b\u00E1o gi\u00E1.");
        }

        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Customer)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Kh\u00F4ng t\u00ECm th\u1EA5y b\u00E1o gi\u00E1.");
        }

        if (quotation.Status == StatusSuperseded)
        {
            throw new InvalidOperationException("B\u00E1o gi\u00E1 \u0111\u00E3 b\u1ECB thay th\u1EBF.");
        }

        if (quotation.Status == StatusCustomerApproved || quotation.Status == StatusCustomerRejected || quotation.Status == StatusCanceled)
        {
            return MapQuotationResponse(quotation);
        }

        var customerEmail = NormalizeText(quotation.ServiceOrder?.Customer?.Email, 255);
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            throw new InvalidOperationException("Kh\u00E1ch h\u00E0ng ch\u01B0a c\u00F3 email h\u1EE3p l\u1EC7 \u0111\u1EC3 g\u1EEDi b\u00E1o gi\u00E1.");
        }

        var now = VietnamTime.Now;
        var previousStatus = quotation.Status;
        quotation.Status = StatusSentToCustomer;
        quotation.CustomerDecisionStatus = ApprovalPending;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = actorStaffId;

        var cleaned = NormalizeText(note, 1000);
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleaned
                : $"{quotation.InternalNotes.Trim()} | {cleaned}";
        }

        var expiresAt = ResolvePortalExpiry(quotation.ValidUntil, now);
        var rawToken = GeneratePublicToken();
        var tokenHash = ComputeTokenHash(rawToken);
        var publicLink = BuildPublicPortalUrl(rawToken);

        var existingActiveLinks = await _context.QuotationAccessLinks
            .Where(x => x.QuotationId == quotation.Id && x.Status == LinkStatusActive)
            .ToListAsync();

        foreach (var link in existingActiveLinks)
        {
            link.Status = LinkStatusRevoked;
            link.UpdatedAt = now;
            link.UpdatedBy = actorStaffId;
        }

        var newLink = new QuotationAccessLink
        {
            QuotationId = quotation.Id,
            TokenHash = tokenHash,
            Status = LinkStatusActive,
            ExpiresAt = expiresAt,
            SentToEmail = customerEmail,
            ClientNote = cleaned,
            CreatedAt = now,
            CreatedBy = actorStaffId,
            UpdatedAt = now,
            UpdatedBy = actorStaffId
        };

        _context.QuotationAccessLinks.Add(newLink);
        await _context.SaveChangesAsync();

        try
        {
            var emailSubject = BuildQuotationEmailSubject(quotation);
            var emailBody = BuildQuotationEmailHtml(quotation, publicLink, cleaned, expiresAt);
            await _emailService.SendEmailAsync(customerEmail, emailSubject, emailBody);
        }
        catch
        {
            newLink.Status = LinkStatusRevoked;
            newLink.UpdatedAt = VietnamTime.Now;
            newLink.UpdatedBy = actorStaffId;
            quotation.Status = previousStatus;
            quotation.UpdatedAt = VietnamTime.Now;
            quotation.UpdatedBy = actorStaffId;
            await _context.SaveChangesAsync();
            throw new InvalidOperationException("Kh\u00F4ng th\u1EC3 g\u1EEDi email b\u00E1o gi\u00E1. Vui l\u00F2ng ki\u1EC3m tra c\u1EA5u h\u00ECnh SMTP.");
        }

        return MapQuotationResponse(quotation);
    }

    public async Task<QuotationPublicViewResponseDto> GetQuotationByPublicTokenAsync(string token)
    {
        var accessLink = await ResolveActiveAccessLinkAsync(token);
        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Customer)
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == accessLink.QuotationId);

        if (quotation == null)
        {
            throw new InvalidOperationException("Kh\u00F4ng t\u00ECm th\u1EA5y b\u00E1o gi\u00E1.");
        }

        var now = VietnamTime.Now;
        accessLink.LastAccessedAt = now;
        if (!accessLink.ViewedAt.HasValue)
        {
            accessLink.ViewedAt = now;
            if (string.Equals(quotation.Status, StatusSentToCustomer, StringComparison.OrdinalIgnoreCase))
            {
                quotation.Status = StatusPendingCustomer;
                quotation.UpdatedAt = now;
                quotation.UpdatedBy = quotation.UpdatedBy ?? quotation.CreatedBy;
            }
        }

        await _context.SaveChangesAsync();
        return MapPublicQuotationResponse(quotation);
    }

    public async Task<QuotationResponseDto> ApplyCustomerDecisionByPublicTokenAsync(string token, QuotationPublicDecisionRequestDto request)
    {
        if (request == null)
        {
            throw new InvalidOperationException("Thi\u1EBFu quy\u1EBFt \u0111\u1ECBnh c\u1EE7a kh\u00E1ch h\u00E0ng.");
        }

        var accessLink = await ResolveActiveAccessLinkAsync(token);
        var quotation = await _context.Quotations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == accessLink.QuotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Kh\u00F4ng t\u00ECm th\u1EA5y b\u00E1o gi\u00E1.");
        }

        var serviceOrder = await _context.ServiceOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == quotation.ServiceOrderId);
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Kh\u00F4ng t\u00ECm th\u1EA5y l\u1EC7nh s\u1EEDa ch\u1EEFa.");
        }

        var updated = await ApplyCustomerDecisionAsync(quotation.Id, new QuotationCustomerDecisionRequestDto
        {
            Approved = request.Approved,
            CustomerId = serviceOrder.CustomerId,
            Note = NormalizeText(request.Note, 1000)
        });

        var now = VietnamTime.Now;
        accessLink.DecidedAt = now;
        accessLink.DecisionCode = request.Approved ? "APPROVED" : "REJECTED";
        accessLink.Status = LinkStatusUsed;
        accessLink.UpdatedAt = now;
        accessLink.UpdatedBy = serviceOrder.CustomerId;

        var siblingActiveLinks = await _context.QuotationAccessLinks
            .Where(x => x.QuotationId == quotation.Id && x.Status == LinkStatusActive && x.Id != accessLink.Id)
            .ToListAsync();
        foreach (var row in siblingActiveLinks)
        {
            row.Status = LinkStatusRevoked;
            row.UpdatedAt = now;
            row.UpdatedBy = serviceOrder.CustomerId;
        }

        await _context.SaveChangesAsync();
        return updated;
    }

    public async Task<QuotationResponseDto> SupersedeQuotationAsync(long quotationId, long actorStaffId, string? note = null)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("M\u00E3 b\u00E1o gi\u00E1 kh\u00F4ng h\u1EE3p l\u1EC7.");
        }

        if (actorStaffId <= 0)
        {
            throw new InvalidOperationException("Thi\u1EBFu nh\u00E2n s\u1EF1 thao t\u00E1c.");
        }

        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Kh\u00F4ng t\u00ECm th\u1EA5y b\u00E1o gi\u00E1.");
        }

        if (quotation.IsAppliedToServiceOrder || quotation.Status == StatusApproved)
        {
            throw new InvalidOperationException("Kh\u00F4ng th\u1EC3 thay th\u1EBF b\u00E1o gi\u00E1 \u0111\u00E3 \u00E1p v\u00E0o l\u1EC7nh s\u1EEDa ch\u1EEFa.");
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

    private static string BuildQuotationTitle(int versionNo)
    {
        var safeVersion = versionNo > 0 ? versionNo : 1;
        return NormalizeText($"B\u00E1o gi\u00E1 V{safeVersion}", 255)!;
    }

    private async Task EnsureQuotationTitleByVersionAsync(Quotation quotation)
    {
        if (EnsureQuotationTitleByVersion(quotation))
        {
            await _context.SaveChangesAsync();
        }
    }

    private static bool EnsureQuotationTitleByVersion(Quotation quotation)
    {
        var expectedTitle = BuildQuotationTitle(quotation.VersionNo);
        if (string.Equals((quotation.Title ?? string.Empty).Trim(), expectedTitle, StringComparison.Ordinal))
        {
            return false;
        }

        quotation.Title = expectedTitle;
        return true;
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

    private DateTime ResolvePortalExpiry(DateTime? quotationValidUntil, DateTime now)
    {
        var configuredDays = _configuration.GetValue<int?>("QuotationPortal:TokenExpiryDays");
        var ttlDays = configuredDays.GetValueOrDefault(DefaultPortalExpiryDays);
        if (ttlDays <= 0)
        {
            ttlDays = DefaultPortalExpiryDays;
        }

        var byConfig = now.AddDays(ttlDays);
        if (quotationValidUntil.HasValue && quotationValidUntil.Value > now)
        {
            return quotationValidUntil.Value <= byConfig ? quotationValidUntil.Value : byConfig;
        }

        return byConfig;
    }

    private static string GeneratePublicToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string ComputeTokenHash(string token)
    {
        var normalized = (token ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private string BuildPublicPortalUrl(string token)
    {
        var configuredBase = (_configuration["QuotationPortal:PublicBaseUrl"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(configuredBase))
        {
            configuredBase = "http://localhost:5173/quote-access";
        }

        var trimmedBase = configuredBase.TrimEnd('/');
        return $"{trimmedBase}/{token}";
    }

    private static string BuildQuotationEmailSubject(Quotation quotation)
    {
        var plate = quotation.ServiceOrder?.Vehicle?.LicensePlate?.Trim();
        var customer = quotation.ServiceOrder?.Customer?.FullName?.Trim();
        var prefix = string.IsNullOrWhiteSpace(plate) ? string.Empty : $" xe {plate}";
        var suffix = string.IsNullOrWhiteSpace(customer) ? string.Empty : $" - KH: {customer}";
        return $"MyGarage Báo giá dịch vụ sửa chữa{prefix}{suffix}";
    }

    private static string BuildQuotationEmailHtml(Quotation quotation, string publicLink, string? clientNote, DateTime expiresAt)
    {
        var customer = WebUtility.HtmlEncode(quotation.ServiceOrder?.Customer?.FullName?.Trim() ?? "Quý khách");
        var plate = WebUtility.HtmlEncode(quotation.ServiceOrder?.Vehicle?.LicensePlate?.Trim() ?? "-");
        var amount = quotation.FinalAmount.ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN"));
        var expiryText = expiresAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN"));
        var safeNote = string.IsNullOrWhiteSpace(clientNote)
            ? string.Empty
            : $"<p style=\"margin:12px 0;padding:10px 12px;background:#f7fafc;border-left:4px solid #2b6cb0;\"><strong>Ghi chú từ CVDV:</strong> {WebUtility.HtmlEncode(clientNote)}</p>";

        return $@"
<div style=""font-family:Arial,sans-serif;max-width:680px;margin:0 auto;color:#1a202c;line-height:1.5;"">
  <h2 style=""margin:0 0 12px;color:#1a365d;"">MyGarage - Báo giá sửa chữa</h2>
  <p>Kính chào anh/chị <strong>{customer}</strong>, MyGarage xin gửi báo giá sửa chữa cho xe <strong>{plate}</strong>.</p>
  {safeNote}
  <p style=""margin:12px 0;"">Tổng chi phí dự kiến: <strong style=""font-size:18px;color:#2b6cb0;"">{amount} đ</strong></p>
  <p style=""margin:12px 0;"">
    <a href=""{WebUtility.HtmlEncode(publicLink)}"" style=""display:inline-block;padding:12px 18px;background:#2b6cb0;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;"">
      XEM CHI TIẾT & DUYỆT BÁO GIÁ
    </a>
  </p>
  <p style=""margin:12px 0;color:#4a5568;"">Liên kết có hiệu lực đến: <strong>{expiryText}</strong></p>
  <p style=""margin:16px 0 0;color:#4a5568;"">Nếu cần hỗ trợ thêm, vui lòng phản hồi lại email này để CVDV tư vấn ngay.</p>
</div>";
    }

    private async Task<QuotationAccessLink> ResolveActiveAccessLinkAsync(string token)
    {
        var normalizedToken = (token ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            throw new InvalidOperationException("Liên kết báo giá không hợp lệ.");
        }

        var tokenHash = ComputeTokenHash(normalizedToken);
        var link = await _context.QuotationAccessLinks
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (link == null)
        {
            throw new InvalidOperationException("Liên kết báo giá không tồn tại hoặc đã bị thu hồi.");
        }

        if (!string.Equals(link.Status, LinkStatusActive, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Liên kết báo giá không còn hiệu lực.");
        }

        if (link.ExpiresAt <= VietnamTime.Now)
        {
            link.Status = LinkStatusExpired;
            link.UpdatedAt = VietnamTime.Now;
            await _context.SaveChangesAsync();
            throw new InvalidOperationException("Báo giá đã hết hiệu lực, vui lòng liên hệ CVDV để cập nhật giá mới.");
        }

        return link;
    }

    private static QuotationPublicViewResponseDto MapPublicQuotationResponse(Quotation quotation)
    {
        var status = quotation.Status ?? StatusDraft;
        var canAct = string.Equals(status, StatusApproved, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, StatusSentToCustomer, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, StatusPendingCustomer, StringComparison.OrdinalIgnoreCase);

        return new QuotationPublicViewResponseDto
        {
            QuotationId = quotation.Id,
            Code = quotation.Code,
            Title = quotation.Title,
            VersionNo = quotation.VersionNo,
            Status = status,
            ValidUntil = quotation.ValidUntil,
            CustomerName = quotation.ServiceOrder?.Customer?.FullName ?? "-",
            CustomerEmail = quotation.ServiceOrder?.Customer?.Email,
            VehicleLicensePlate = quotation.ServiceOrder?.Vehicle?.LicensePlate ?? "-",
            FinalAmount = quotation.FinalAmount,
            TotalInsuranceAmount = quotation.TotalInsuranceAmount,
            TotalCustomerAmount = quotation.TotalCustomerAmount,
            CanApprove = canAct,
            CanReject = canAct,
            Details = (quotation.QuotationDetails ?? new List<QuotationDetail>())
                .OrderBy(x => x.Id)
                .Select(x => new QuotationPublicDetailResponseDto
                {
                    Id = x.Id,
                    ItemType = x.ItemType,
                    ItemName = x.ItemName,
                    Quantity = x.Quantity,
                    UnitPriceAtTime = x.UnitPriceAtTime,
                    FinalAmount = x.FinalAmount,
                    IsMandatory = x.IsMandatory
                })
                .ToList()
        };
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
        var normalizedPayerType = string.IsNullOrWhiteSpace(dto.PayerType) ? "CUSTOMER" : dto.PayerType.Trim().ToUpperInvariant();
        var split = CalculateSplitBilling(finalAmount, dto.InsuranceCoveredAmount ?? dto.InsuranceRequestedAmount, dto.CustomerCoveredAmount ?? dto.CustomerCoPayAmount, normalizedPayerType);

        return new QuotationDetail
        {
            ServiceOrderId = serviceOrderId,
            DiagnosisRecommendationId = dto.DiagnosisRecommendationId,
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
            PayerType = split.PayerType,
            InsuranceCompanyId = dto.InsuranceCompanyId,
            InsuranceCoveredAmount = split.InsuranceCoveredAmount,
            CustomerCoveredAmount = split.CustomerCoveredAmount,
            InsuranceRequestedAmount = split.InsuranceCoveredAmount,
            CustomerCoPayAmount = split.CustomerCoveredAmount,
            ApprovalStatus = ApprovalPending,
            IsBlocking = dto.IsBlocking,
            IsMandatory = dto.IsMandatory,
            EstimatedMinutes = dto.EstimatedMinutes,
            IsReturnOldPart = dto.IsReturnOldPart,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorStaffId,
            UpdatedBy = actorStaffId
        };
    }

    private static (decimal InsuranceCoveredAmount, decimal CustomerCoveredAmount, string PayerType) CalculateSplitBilling(
        decimal totalLineAmount,
        decimal? insuranceCoveredInput,
        decimal? customerCoveredInput,
        string? payerTypeInput)
    {
        var total = Math.Max(0m, Math.Round(totalLineAmount, 2, MidpointRounding.AwayFromZero));
        var normalizedPayer = (payerTypeInput ?? string.Empty).Trim().ToUpperInvariant();

        decimal insuranceCovered;
        decimal customerCovered;

        if (insuranceCoveredInput.HasValue)
        {
            insuranceCovered = ClampCurrency(insuranceCoveredInput.Value, 0m, total);
            customerCovered = Math.Round(total - insuranceCovered, 2, MidpointRounding.AwayFromZero);
        }
        else if (customerCoveredInput.HasValue)
        {
            customerCovered = ClampCurrency(customerCoveredInput.Value, 0m, total);
            insuranceCovered = Math.Round(total - customerCovered, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            if (normalizedPayer == "INSURANCE")
            {
                insuranceCovered = total;
                customerCovered = 0m;
            }
            else if (normalizedPayer == "SPLIT")
            {
                insuranceCovered = Math.Round(total / 2m, 2, MidpointRounding.AwayFromZero);
                customerCovered = Math.Round(total - insuranceCovered, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                insuranceCovered = 0m;
                customerCovered = total;
            }
        }

        var inferredPayer = insuranceCovered <= 0m
            ? "CUSTOMER"
            : customerCovered <= 0m
                ? "INSURANCE"
                : "SPLIT";

        return (insuranceCovered, customerCovered, inferredPayer);
    }

    private static decimal ClampCurrency(decimal value, decimal min, decimal max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static ServiceOrderDetail BuildServiceOrderDetailFromQuotation(QuotationDetail detail, long serviceOrderId, long actorStaffId, DateTime now)
    {
        var normalizedItemType = NormalizeServiceOrderDetailItemType(detail.ItemType);
        var itemId = detail.ItemId;
        var standardServiceId = normalizedItemType == "LABOR" && itemId.HasValue && itemId.Value > 0
            ? itemId
            : null;

        return new ServiceOrderDetail
        {
            ServiceOrderId = serviceOrderId,
            QuotationDetailId = detail.Id,
            DiagnosisRecommendationId = detail.DiagnosisRecommendationId,
            StandardServiceId = standardServiceId,
            ItemType = normalizedItemType,
            ItemId = itemId,
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
            InsuranceRequestedAmount = detail.InsuranceCoveredAmount ?? detail.InsuranceRequestedAmount,
            CustomerCoPayAmount = detail.CustomerCoveredAmount ?? detail.CustomerCoPayAmount,
            Status = nameof(ServiceOrderDetailStatusCode.APPROVED),
            IsReturnOldPart = detail.IsReturnOldPart,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorStaffId,
            UpdatedBy = actorStaffId
        };
    }

    private static string NormalizeServiceOrderDetailItemType(string? itemType)
    {
        var normalized = (itemType ?? string.Empty).Trim().ToUpperInvariant();

        return normalized switch
        {
            "PART" or "PARTS" or "PRODUCT" or "MATERIAL" or "INVENTORY" => "PART",
            "SERVICE" or "STANDARD_SERVICE" or "LABOR" => "LABOR",
            _ => "LABOR"
        };
    }

    private static bool IsMandatoryByUrgency(string? urgency)
    {
        var normalized = (urgency ?? string.Empty).Trim().ToUpperInvariant();
        return normalized is "CRITICAL" or "SAFETY" or "SAFETY_CRITICAL" or "MANDATORY";
    }

    private static (string ItemType, long? ItemId, string ItemName, decimal UnitPrice, decimal? CostPrice, decimal TaxRate) MapRecommendationToPrice(
        DiagnoseRecommendation recommendation,
        IReadOnlyDictionary<long, StandardService> serviceById,
        IReadOnlyDictionary<long, Product> productById)
    {
        if (recommendation.StandardServiceId.HasValue &&
            recommendation.StandardServiceId.Value > 0 &&
            serviceById.TryGetValue(recommendation.StandardServiceId.Value, out var standardService))
        {
            return (
                ItemType: "LABOR",
                ItemId: standardService.Id,
                ItemName: standardService.Name,
                UnitPrice: standardService.BasePrice,
                CostPrice: standardService.ConsumableCost,
                TaxRate: standardService.TaxRate ?? 0m
            );
        }

        if (recommendation.InventoryItemId.HasValue &&
            recommendation.InventoryItemId.Value > 0 &&
            productById.TryGetValue(recommendation.InventoryItemId.Value, out var product))
        {
            return (
                ItemType: "PART",
                ItemId: product.Id,
                ItemName: product.Name,
                UnitPrice: product.ServicePrice ?? product.RetailPrice ?? 0m,
                CostPrice: product.CostPrice,
                TaxRate: product.TaxRate ?? 0m
            );
        }

        var fallbackName = string.IsNullOrWhiteSpace(recommendation.TechnicianNotes)
            ? "UNNAMED_ITEM"
            : recommendation.TechnicianNotes!.Trim();
        return (
            ItemType: "LABOR",
            ItemId: recommendation.StandardServiceId ?? recommendation.InventoryItemId,
            ItemName: fallbackName,
            UnitPrice: 0m,
            CostPrice: null,
            TaxRate: 0m
        );
    }

    private static (decimal UnitPrice, decimal? CostPrice, decimal TaxRate) RepriceQuotationDetail(
        QuotationDetail detail,
        IReadOnlyDictionary<long, StandardService> serviceById,
        IReadOnlyDictionary<long, Product> productById)
    {
        var normalizedItemType = NormalizeServiceOrderDetailItemType(detail.ItemType);
        if (detail.ItemId.HasValue &&
            detail.ItemId.Value > 0 &&
            string.Equals(normalizedItemType, "LABOR", StringComparison.OrdinalIgnoreCase) &&
            serviceById.TryGetValue(detail.ItemId.Value, out var standardService))
        {
            return (standardService.BasePrice, standardService.ConsumableCost, standardService.TaxRate ?? detail.TaxRate);
        }

        if (detail.ItemId.HasValue &&
            detail.ItemId.Value > 0 &&
            string.Equals(normalizedItemType, "PART", StringComparison.OrdinalIgnoreCase) &&
            productById.TryGetValue(detail.ItemId.Value, out var product))
        {
            return (product.ServicePrice ?? product.RetailPrice ?? detail.UnitPriceAtTime, product.CostPrice, product.TaxRate ?? detail.TaxRate);
        }

        return (detail.UnitPriceAtTime, detail.CostPriceAtTime, detail.TaxRate);
    }

    private static void RecalculateQuotationTotals(Quotation quotation)
    {
        var details = quotation.QuotationDetails ?? new List<QuotationDetail>();
        quotation.SubTotalAmount = details.Sum(x => x.SubTotal);
        quotation.TotalDiscount = details.Sum(x => x.DiscountValue);
        quotation.TaxAmount = details.Sum(x => x.TaxAmount);
        quotation.FinalAmount = details.Sum(x => x.FinalAmount);
        quotation.TotalInsuranceAmount = details.Sum(x =>
            x.InsuranceCoveredAmount
            ?? x.InsuranceRequestedAmount
            ?? (string.Equals(x.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase) ? x.FinalAmount : 0m));
        quotation.TotalCustomerAmount = details.Sum(x =>
            x.CustomerCoveredAmount
            ?? x.CustomerCoPayAmount
            ?? (!string.Equals(x.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase)
                ? x.FinalAmount
                : Math.Max(0m, x.FinalAmount - (x.InsuranceCoveredAmount ?? x.InsuranceRequestedAmount ?? 0m))));
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

    private static QuotationResponseDto MapQuotationResponse(Quotation quotation, IReadOnlyCollection<string>? warnings = null)
    {
        return new QuotationResponseDto
        {
            Id = quotation.Id,
            Code = quotation.Code,
            ServiceOrderId = quotation.ServiceOrderId,
            DiagnosisId = quotation.DiagnosisId,
            SourceSnapshotId = quotation.SourceSnapshotId,
            Title = quotation.Title,
            QuotationType = quotation.QuotationType,
            VersionNo = quotation.VersionNo,
            Status = quotation.Status,
            InsuranceClaimNumber = quotation.InsuranceClaimNumber,
            TechnicalApprovedBy = quotation.TechnicalApprovedBy,
            TechnicalApprovedAt = quotation.TechnicalApprovedAt,
            TeamLeaderApprovalStatus = quotation.TeamLeaderApprovalStatus,
            FinalApprovedBy = quotation.FinalApprovedBy,
            FinalApprovedAt = quotation.FinalApprovedAt,
            FinalApprovalStatus = quotation.FinalApprovalStatus,
            CustomerDecisionBy = quotation.CustomerDecisionBy,
            CustomerDecisionAt = quotation.CustomerDecisionAt,
            CustomerDecisionStatus = quotation.CustomerDecisionStatus,
            ValidUntil = quotation.ValidUntil,
            SubTotalAmount = quotation.SubTotalAmount,
            TotalDiscount = quotation.TotalDiscount,
            TaxAmount = quotation.TaxAmount,
            FinalAmount = quotation.FinalAmount,
            TotalInsuranceAmount = quotation.TotalInsuranceAmount,
            TotalCustomerAmount = quotation.TotalCustomerAmount,
            Warnings = warnings?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList() ?? new List<string>(),
            IsAppliedToServiceOrder = quotation.IsAppliedToServiceOrder,
            AppliedAt = quotation.AppliedAt,
            Details = (quotation.QuotationDetails ?? new List<QuotationDetail>())
                .OrderBy(x => x.Id)
                .Select(x => new QuotationDetailResponseDto
                {
                    Id = x.Id,
                    DiagnosisRecommendationId = x.DiagnosisRecommendationId,
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
                    InsuranceCoveredAmount = x.InsuranceCoveredAmount,
                    CustomerCoveredAmount = x.CustomerCoveredAmount,
                    InsuranceRequestedAmount = x.InsuranceRequestedAmount,
                    CustomerCoPayAmount = x.CustomerCoPayAmount,
                    ApprovalStatus = x.ApprovalStatus,
                    RejectReason = x.RejectReason,
                    IsBlocking = x.IsBlocking,
                    IsMandatory = x.IsMandatory,
                    EstimatedMinutes = x.EstimatedMinutes,
                    IsReturnOldPart = x.IsReturnOldPart
                })
                .ToList()
        };
    }
}




