using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Hubs;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class QuotationWorkflowService : IQuotationWorkflowService
{
    private readonly IGarageContext _context;
    private readonly IServiceOrderDetailService _serviceOrderDetailService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IHubContext<ChatHub> _chatHubContext;

    private const string StatusDraft = nameof(QuotationStatusCode.DRAFT);
    private const string StatusWaitingApproval = nameof(QuotationStatusCode.WAITING_APPROVAL);
    private const string StatusSentToCustomer = nameof(QuotationStatusCode.SENT_TO_CUSTOMER);
    private const string StatusPendingInsurance = nameof(QuotationStatusCode.PENDING_INSURANCE);
    private const string StatusInsuranceApproved = nameof(QuotationStatusCode.INSURANCE_APPROVED);
    private const string StatusPendingCustomer = nameof(QuotationStatusCode.PENDING_CUSTOMER);
    private const string StatusCustomerApproved = nameof(QuotationStatusCode.CUSTOMER_APPROVED);
    private const string StatusCustomerRejected = nameof(QuotationStatusCode.CUSTOMER_REJECTED);
    private const string StatusRejected = nameof(QuotationStatusCode.REJECTED);
    private const string StatusSuperseded = nameof(QuotationStatusCode.SUPERSEDED);
    private const string StatusCanceled = nameof(QuotationStatusCode.CANCELED);
    private const string LegacyStatusCancelled = "CANCELLED";

    private const string ApprovalPending = nameof(QuotationDetailApprovalStatusCode.PENDING);
    private const string ApprovalApproved = nameof(QuotationDetailApprovalStatusCode.APPROVED);
    private const string ApprovalRejected = nameof(QuotationDetailApprovalStatusCode.REJECTED);
    private const string DetailStatusActive = "ACTIVE";
    private const string DetailStatusCanceled = "CANCELED";
    private const string InsuranceChangeTypeNone = "NONE";
    private const string InsuranceChangeTypeIncrease = "INCREASE";
    private const string InsuranceChangeTypeDecrease = "DECREASE";
    private const string LinkStatusActive = "ACTIVE";
    private const string LinkStatusExpired = "EXPIRED";
    private const string LinkStatusRevoked = "REVOKED";
    private const string LinkStatusUsed = "USED";
    private const int DefaultPortalExpiryDays = 15;
    private const int OtpLength = 6;
    private const int OtpTtlSeconds = 60;
    private const int OtpMaxAttempts = 5;
    private const int OtpResendCooldownSeconds = 0;
    private const decimal DefaultMaxDiscountPercentWithoutManager = 5m;
    private const decimal DefaultHighValueThreshold = 50000000m;
    private const string TechnicalApprovalPending = "PENDING";
    private const string TechnicalApprovalApproved = "APPROVED";
    private const string TechnicalApprovalRejected = "REJECTED";
    private const string FinancialApprovalNotRequired = "NOT_REQUIRED";
    private const string FinancialApprovalPending = "PENDING";
    private const string FinancialApprovalApproved = "APPROVED";
    private const string FinancialApprovalRejected = "REJECTED";
    private const string ApprovalActionTypeTechnical = "TECHNICAL";
    private const string ApprovalActionTypeFinancial = "FINANCIAL";
    private const string StatusPendingBranchManager = "PENDING_BRANCH_MANAGER";

    public QuotationWorkflowService(
        IGarageContext context,
        IServiceOrderDetailService serviceOrderDetailService,
        IEmailService emailService,
        IConfiguration configuration,
        IHubContext<ChatHub> chatHubContext)
    {
        _context = context;
        _serviceOrderDetailService = serviceOrderDetailService;
        _emailService = emailService;
        _configuration = configuration;
        _chatHubContext = chatHubContext;
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

        var serviceOrder = await _context.ServiceOrders
            .AsNoTracking()
            .Where(x => x.Id == serviceOrderId)
            .Select(x => new
            {
                x.Id,
                x.ServiceAdvisorId,
                x.ReceptionistId
            })
            .FirstOrDefaultAsync();
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Kh\u00F4ng t\u00ECm th\u1EA5y l\u1EC7nh s\u1EEDa ch\u1EEFa.");
        }

        long? resolvedDiagnosisId = request.DiagnosisId.HasValue && request.DiagnosisId.Value > 0
            ? request.DiagnosisId.Value
            : null;
        long? resolvedDiagnosisAssignmentId = request.DiagnosisAssignmentId.HasValue && request.DiagnosisAssignmentId.Value > 0
            ? request.DiagnosisAssignmentId.Value
            : null;

        if (resolvedDiagnosisAssignmentId.HasValue)
        {
            var assignmentInfo = await _context.DiagnosisAssignments.AsNoTracking()
                .Where(x => x.Id == resolvedDiagnosisAssignmentId.Value)
                .Select(x => new
                {
                    x.Id,
                    x.DiagnosisId,
                    DiagnosisServiceOrderId = x.Diagnosis.ServiceOrderId
                })
                .FirstOrDefaultAsync();
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
            var diagnosisBelongsToServiceOrder = await _context.Diagnoses.AsNoTracking()
                .AnyAsync(x => x.Id == resolvedDiagnosisId.Value && x.ServiceOrderId == serviceOrderId);
            if (!diagnosisBelongsToServiceOrder)
            {
                throw new InvalidOperationException("DiagnosisId không thuộc lệnh sửa chữa hiện tại.");
            }

            if (!resolvedDiagnosisAssignmentId.HasValue)
            {
                resolvedDiagnosisAssignmentId = await _context.DiagnosisAssignments.AsNoTracking()
                    .Where(x => x.DiagnosisId == resolvedDiagnosisId.Value)
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                    .Select(x => (long?)x.Id)
                    .FirstOrDefaultAsync();
            }
        }

        if (!resolvedDiagnosisAssignmentId.HasValue || resolvedDiagnosisAssignmentId.Value <= 0)
        {
            throw new InvalidOperationException("Báo giá phải gắn với một phiên bản chuẩn đoán hợp lệ (DiagnosisAssignmentId).");
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
            DiagnosisId = resolvedDiagnosisId,
            DiagnosisAssignmentId = resolvedDiagnosisAssignmentId,
            Title = BuildQuotationTitle(nextVersion),
            QuotationType = normalizedType,
            VersionNo = nextVersion,
            Status = request.SubmitForApproval ? StatusWaitingApproval : StatusDraft,
            InsuranceClaimNumber = NormalizeText(request.InsuranceClaimNumber, 100),
            ValidUntil = request.ValidUntil,
            ServiceAdvisorId = ResolveServiceAdvisorForQuotation(
                request.ServiceAdvisorId,
                serviceOrder.ServiceAdvisorId,
                serviceOrder.ReceptionistId),
            CustomerNotes = NormalizeText(request.CustomerNotes, 1000),
            InternalNotes = NormalizeText(request.InternalNotes, 1000),
            TechnicalApprovalStatus = TechnicalApprovalPending,
            RequiresFinancialApproval = false,
            FinancialApprovalStatus = FinancialApprovalNotRequired,
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
        quotation.InsuranceChangeType = await ResolveInsuranceChangeTypeAsync(quotation, null);

        var priorOpenQuotations = await _context.Quotations
            .Where(x =>
                x.ServiceOrderId == serviceOrderId &&
                x.Status != StatusSuperseded &&
                x.Status != StatusCustomerApproved)
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
        var latestAssignmentByDiagnosisId = latestAssignmentIdsByDiagnosis
            .GroupBy(x => x.DiagnosisId)
            .ToDictionary(x => x.Key, x => x.First().Id);
        var latestAssignmentIds = latestAssignmentByDiagnosisId.Values.ToList();

        var recommendations = await _context.DiagnosisRecommendations.AsNoTracking()
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

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<QuotationResponseDto>(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
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
                    DiagnosisAssignmentId = latestAssignmentByDiagnosisId.TryGetValue(latestDiagnosis.Id, out var latestAssignmentId)
                        ? latestAssignmentId
                        : null,
                    Title = BuildQuotationTitle(nextSnapshotVersion),
                    QuotationType = nameof(QuotationTypeCode.INSURANCE),
                    ServiceAdvisorId = ResolveServiceAdvisorForQuotation(
                        null,
                        serviceOrder.ServiceAdvisorId,
                        serviceOrder.ReceptionistId),
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
                quotationEntity.CustomerDecisionStatus = ApprovalPending;
                quotationEntity.UpdatedBy = actorStaffId;
                quotationEntity.UpdatedAt = now;
                await _context.SaveChangesAsync();

                if (tx != null) await tx.CommitAsync();
                return await GetQuotationAsync(quotationEntity.Id) ?? MapQuotationResponse(quotationEntity);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync();
                throw;
            }
        });
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
        if (quotation == null)
        {
            return null;
        }

        var supplementaryMetaByDetailId = await BuildSupplementaryMetaByQuotationDetailIdAsync(quotation.ServiceOrderId);
        return MapQuotationResponse(quotation, null, supplementaryMetaByDetailId);
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

        var supplementaryMetaByDetailId = await BuildSupplementaryMetaByQuotationDetailIdAsync(serviceOrderId);
        return rows.Select(x => MapQuotationResponse(x, null, supplementaryMetaByDetailId)).ToList();
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

            var repriced = RepriceQuotationDetail(detail, serviceById, productById);
            cloneRequest.Items.Add(new QuotationCreateItemDto
            {
                SourceQuotationDetailId = detail.Id,
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
                IsBlocking = detail.IsBlocking,
                IsMandatory = detail.IsMandatory,
                EstimatedMinutes = detail.EstimatedMinutes,
                IsReturnOldPart = detail.IsReturnOldPart,
                Status = markCanceled ? DetailStatusCanceled : (string.IsNullOrWhiteSpace(detail.Status) ? DetailStatusActive : detail.Status),
                CancelReason = markCanceled
                    ? (NormalizeText(request.Note, 500) ?? "Khách yêu cầu hủy hạng mục")
                    : detail.CancelReason,
                CanceledBy = markCanceled ? request.ActorStaffId : detail.CanceledBy
            });
        }

        if (cloneRequest.Items.Count == 0)
        {
            throw new InvalidOperationException("Quotation clone has no remaining items.");
        }

        var cloned = await CreateQuotationAsync(source.ServiceOrderId, cloneRequest, request.ActorStaffId);
        var clonedEntity = await _context.Quotations.FirstAsync(x => x.Id == cloned.Id);
        clonedEntity.SourceSnapshotId = source.SourceSnapshotId;
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
            warnings.Add("Tổng bồi thường đã thay đổi. Vui lòng cập nhật với Giám định viên.");
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

    public async Task<QuotationResponseDto> CancelQuotationDetailAsync(long quotationId, long quotationDetailId, QuotationDetailCancelRequestDto request)
    {
        if (quotationId <= 0 || quotationDetailId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá hoặc mã hạng mục không hợp lệ.");
        }

        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự thực hiện hủy hạng mục.");
        }

        var cancelReason = NormalizeText(request.CancelReason, 500);
        if (string.IsNullOrWhiteSpace(cancelReason))
        {
            throw new InvalidOperationException("Bắt buộc nhập lý do hủy hạng mục.");
        }

        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.IsAppliedToServiceOrder ||
            string.Equals(quotation.Status, StatusCustomerApproved, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Không được hủy hạng mục trên báo giá đã duyệt và đã đồng bộ vào SOD.");
        }

        var detail = quotation.QuotationDetails.FirstOrDefault(x => x.Id == quotationDetailId);
        if (detail == null)
        {
            throw new InvalidOperationException("Không tìm thấy hạng mục cần hủy.");
        }

        if (string.Equals(detail.Status, DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
        {
            return MapQuotationResponse(quotation);
        }

        var previousStatus = quotation.Status;
        var hadInsuranceCoverage = ResolveInsuranceCoveredAmount(detail) > 0m;
        var now = VietnamTime.Now;
        detail.Status = DetailStatusCanceled;
        detail.CancelReason = cancelReason;
        detail.CanceledBy = request.ActorStaffId;
        detail.InsuranceCoveredAmount = 0m;
        detail.CustomerCoveredAmount = 0m;
        detail.PayerType = "CUSTOMER";
        detail.UpdatedAt = now;
        detail.UpdatedBy = request.ActorStaffId;

        quotation.CustomerDecisionStatus = ApprovalPending;
        quotation.IsReadyForSplitBilling = false;
        quotation.SplitBillingReadyAt = null;
        if (hadInsuranceCoverage)
        {
            quotation.Status = StatusPendingInsurance;
            quotation.InsuranceApprovalEvidenceUrl = null;
        }
        else
        {
            var requiresInsuranceApproval = IsInsuranceApprovalRequiredForQuotation(quotation);
            if (!requiresInsuranceApproval)
            {
                quotation.Status = StatusInsuranceApproved;
            }
            else
            {
                quotation.Status = previousStatus;
            }
        }
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = request.ActorStaffId;
        RecalculateQuotationTotals(quotation);
        quotation.InsuranceChangeType = await ResolveInsuranceChangeTypeAsync(quotation, null);

        var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == quotation.ServiceOrderId);
        if (serviceOrder != null)
        {
            serviceOrder.InsuranceQuoteState = quotation.Status;
            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = request.ActorStaffId;
        }

        await _context.SaveChangesAsync();
        await NotifyCancellationAlertAsync(quotation, detail, request.ActorStaffId, now);

        return MapQuotationResponse(quotation);
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
            string.Equals(quotation.Status, StatusInsuranceApproved, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, StatusPendingCustomer, StringComparison.OrdinalIgnoreCase) ||
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
            if (string.Equals(detail.Status, DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

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

        var requiresInsuranceApproval = IsInsuranceApprovalRequiredForQuotation(quotation);
        quotation.Status = requiresInsuranceApproval ? StatusPendingInsurance : StatusInsuranceApproved;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = request.ActorStaffId;
        RecalculateQuotationTotals(quotation);
        quotation.InsuranceChangeType = await ResolveInsuranceChangeTypeAsync(quotation, null);

        var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == quotation.ServiceOrderId);
        if (serviceOrder != null)
        {
            serviceOrder.InsuranceQuoteState = quotation.Status;
            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = request.ActorStaffId;
        }

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

        if (quotation.Status == StatusCustomerApproved && quotation.IsAppliedToServiceOrder)
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
            if (string.Equals(detail.Status, DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

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
            .Where(x =>
                !string.Equals(x.Status, DetailStatusCanceled, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ApprovalStatus, ApprovalApproved, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var technicalApproved = approvedDetails.Count > 0;
        quotation.Status = technicalApproved ? StatusPendingInsurance : StatusRejected;
        quotation.ApprovedBy = request.ApprovedBy;
        quotation.ApprovedAt = now;
        quotation.TechnicalApprovedBy = request.ApprovedBy;
        quotation.TechnicalApprovedAt = now;
        quotation.TechnicalApprovalStatus = technicalApproved ? TechnicalApprovalApproved : TechnicalApprovalRejected;
        quotation.RequiresFinancialApproval = false;
        quotation.FinancialApprovalStatus = FinancialApprovalNotRequired;
        quotation.FinancialApprovedBy = null;
        quotation.FinancialApprovedAt = null;
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

        if (technicalApproved)
        {
            var financialApprovalRequired = await IsFinancialApprovalRequiredAsync(quotation);
            if (financialApprovalRequired)
            {
                quotation.RequiresFinancialApproval = true;
                quotation.FinancialApprovalStatus = FinancialApprovalPending;
                quotation.Status = StatusPendingBranchManager;
            }
        }

        _context.QuotationApprovalActions.Add(new QuotationApprovalAction
        {
            QuotationId = quotation.Id,
            ApprovalType = ApprovalActionTypeTechnical,
            Decision = technicalApproved ? TechnicalApprovalApproved : TechnicalApprovalRejected,
            ActorStaffId = request.ApprovedBy,
            Note = NormalizeText(request.Note, 1000),
            CreatedAt = now
        });

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

        var serviceOrder = await _context.ServiceOrders
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == quotation.ServiceOrderId);
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Kh\u00F4ng t\u00ECm th\u1EA5y l\u1EC7nh s\u1EEDa ch\u1EEFa.");
        }

        EnsureCustomerDecisionAllowed(quotation, serviceOrder);

        var now = VietnamTime.Now;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var cleanedNote = request.Note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleanedNote
                : $"{quotation.InternalNotes.Trim()} | {cleanedNote}";
        }

        if (!string.Equals(quotation.Status, StatusPendingCustomer, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Approved && string.Equals(quotation.Status, StatusCustomerApproved, StringComparison.OrdinalIgnoreCase))
            {
                return MapQuotationResponse(quotation);
            }

            if (!request.Approved && string.Equals(quotation.Status, StatusCustomerRejected, StringComparison.OrdinalIgnoreCase))
            {
                return MapQuotationResponse(quotation);
            }

            throw new InvalidOperationException("Báo giá chưa ở trạng thái chờ khách hàng xác nhận.");
        }

        if (request.Approved)
        {
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
            quotation.IsReadyForSplitBilling = true;
            quotation.SplitBillingReadyAt = now;
            quotation.UpdatedAt = now;
            quotation.UpdatedBy = actorId;
            serviceOrder.InsuranceQuoteState = StatusCustomerApproved;
            await ApplyQuotationToServiceOrderAsync(quotation, serviceOrder, actorId, now);
            await TryNotifyInsuranceApprovalChainAsync(quotation, serviceOrder, true);
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
        quotation.IsReadyForSplitBilling = false;
        quotation.SplitBillingReadyAt = null;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = actorId;
        serviceOrder.InsuranceQuoteState = StatusCustomerRejected;
        serviceOrder.Status = nameof(ServiceOrderLifecycleStatus.DIAGNOSING);
        serviceOrder.UpdatedAt = now;
        serviceOrder.UpdatedBy = actorId;

        await _context.SaveChangesAsync();
        await TryNotifyInsuranceApprovalChainAsync(quotation, serviceOrder, false);
        return MapQuotationResponse(quotation);
    }

    private async Task ApplyQuotationToServiceOrderAsync(Quotation quotation, ServiceOrder serviceOrder, long actorId, DateTime now)
    {
        if (quotation.IsAppliedToServiceOrder)
        {
            return;
        }

        var approvedDetails = quotation.QuotationDetails
            .Where(x =>
                !string.Equals(x.Status, DetailStatusCanceled, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ApprovalStatus, ApprovalApproved, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (approvedDetails.Count == 0)
        {
            throw new InvalidOperationException("Kh\u00F4ng c\u00F3 h\u1EA1ng m\u1EE5c \u0111\u01B0\u1EE3c duy\u1EC7t \u0111\u1EC3 \u00E1p v\u00E0o l\u1EC7nh s\u1EEDa ch\u1EEFa.");
        }

        var existingServiceOrderDetails = await _context.ServiceOrderDetails
            .Where(x => x.ServiceOrderId == serviceOrder.Id)
            .ToListAsync();
        var isTechnicianSupplementary = await IsTechnicianSupplementaryQuotationAsync(quotation, serviceOrder.Id);
        var detailByQuotationDetailId = existingServiceOrderDetails
            .Where(x => x.QuotationDetailId.HasValue && x.QuotationDetailId.Value > 0)
            .GroupBy(x => x.QuotationDetailId!.Value)
            .ToDictionary(x => x.Key, x => x.First());

        var rowsToCreate = new List<ServiceOrderDetail>();
        foreach (var approvedDetail in approvedDetails)
        {
            if (detailByQuotationDetailId.ContainsKey(approvedDetail.Id))
            {
                continue;
            }

            ServiceOrderDetail? existingFromSource = null;
            if (approvedDetail.SourceQuotationDetailId.HasValue && approvedDetail.SourceQuotationDetailId.Value > 0)
            {
                detailByQuotationDetailId.TryGetValue(approvedDetail.SourceQuotationDetailId.Value, out existingFromSource);
            }

            if (existingFromSource != null)
            {
                EnsureServiceOrderDetailAdjustable(existingFromSource, approvedDetail.ItemName);
                ApplyApprovedQuotationToExistingServiceOrderDetail(existingFromSource, approvedDetail, quotation, actorId, now);
                detailByQuotationDetailId[approvedDetail.Id] = existingFromSource;
                continue;
            }

            var created = BuildServiceOrderDetailFromQuotation(
                approvedDetail,
                quotation,
                serviceOrder.Id,
                actorId,
                now,
                isTechnicianSupplementary);
            rowsToCreate.Add(created);
            detailByQuotationDetailId[approvedDetail.Id] = created;
        }

        var canceledDetails = quotation.QuotationDetails
            .Where(x =>
                string.Equals(x.Status, DetailStatusCanceled, StringComparison.OrdinalIgnoreCase) &&
                x.SourceQuotationDetailId.HasValue &&
                x.SourceQuotationDetailId.Value > 0)
            .ToList();
        foreach (var canceledDetail in canceledDetails)
        {
            if (!detailByQuotationDetailId.TryGetValue(canceledDetail.SourceQuotationDetailId!.Value, out var existingFromSource))
            {
                continue;
            }

            EnsureServiceOrderDetailAdjustable(existingFromSource, canceledDetail.ItemName);
            existingFromSource.Status = nameof(ServiceOrderDetailStatusCode.CANCELED);
            existingFromSource.Note = MergeCancellationNote(existingFromSource.Note, canceledDetail.CancelReason);
            existingFromSource.QuotationDetailId = canceledDetail.Id;
            existingFromSource.SourceQuotationId = quotation.Id;
            existingFromSource.UpdatedBy = actorId;
            existingFromSource.UpdatedAt = now;
            detailByQuotationDetailId[canceledDetail.Id] = existingFromSource;
        }

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

    private static void ApplyApprovedQuotationToExistingServiceOrderDetail(
        ServiceOrderDetail existingDetail,
        QuotationDetail approvedDetail,
        Quotation quotation,
        long actorId,
        DateTime now)
    {
        var normalizedItemType = NormalizeServiceOrderDetailItemType(approvedDetail.ItemType);
        var normalizedPayerType = NormalizeServiceOrderDetailPayerType(
            approvedDetail.PayerType,
            approvedDetail.InsuranceCoveredAmount,
            approvedDetail.CustomerCoveredAmount);
        var standardServiceId = normalizedItemType == "LABOR" && approvedDetail.ItemId.HasValue && approvedDetail.ItemId.Value > 0
            ? approvedDetail.ItemId
            : null;

        existingDetail.QuotationDetailId = approvedDetail.Id;
        existingDetail.StandardServiceId = standardServiceId;
        existingDetail.ItemType = normalizedItemType;
        existingDetail.ItemId = approvedDetail.ItemId;
        existingDetail.ItemName = approvedDetail.ItemName;
        existingDetail.Description = approvedDetail.Description;
        existingDetail.Quantity = approvedDetail.Quantity <= 0 ? 1m : approvedDetail.Quantity;
        existingDetail.EstimatedMinutes = approvedDetail.EstimatedMinutes;
        existingDetail.CostPriceAtTime = approvedDetail.CostPriceAtTime;
        existingDetail.UnitPriceAtTime = approvedDetail.UnitPriceAtTime;
        existingDetail.SubTotal = approvedDetail.SubTotal;
        existingDetail.DiscountValue = approvedDetail.DiscountValue;
        existingDetail.TaxRate = approvedDetail.TaxRate;
        existingDetail.TaxAmount = approvedDetail.TaxAmount;
        existingDetail.FinalAmount = approvedDetail.FinalAmount;
        existingDetail.PayerType = normalizedPayerType;
        existingDetail.InsuranceCompanyId = approvedDetail.InsuranceCompanyId;
        existingDetail.InsuranceRequestedAmount = approvedDetail.InsuranceCoveredAmount;
        existingDetail.CustomerCoPayAmount = approvedDetail.CustomerCoveredAmount;
        existingDetail.Status = nameof(ServiceOrderDetailStatusCode.APPROVED);
        existingDetail.IsReturnOldPart = approvedDetail.IsReturnOldPart;
        existingDetail.SourceQuotationId = quotation.Id;
        existingDetail.UpdatedAt = now;
        existingDetail.UpdatedBy = actorId;
    }

    private static void EnsureServiceOrderDetailAdjustable(ServiceOrderDetail detail, string? itemName)
    {
        if (detail == null)
        {
            return;
        }

        if ((detail.IssuedQuantity ?? 0m) > 0m)
        {
            throw new InvalidOperationException(
                $"Không thể giảm/hủy hạng mục '{itemName ?? detail.ItemName}' vì vật tư đã xuất kho.");
        }

        var status = (detail.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (status == nameof(ServiceOrderDetailStatusCode.IN_PROGRESS) ||
            status == nameof(ServiceOrderDetailStatusCode.COMPLETED) ||
            status == nameof(ServiceOrderDetailStatusCode.SETTLED) ||
            status == nameof(ServiceOrderDetailStatusCode.SUBMITTED))
        {
            throw new InvalidOperationException(
                $"Không thể giảm/hủy hạng mục '{itemName ?? detail.ItemName}' vì đã bắt đầu thi công hoặc đã quyết toán.");
        }
    }

    private static string MergeCancellationNote(string? currentNote, string? cancelReason)
    {
        var reason = NormalizeText(cancelReason, 500);
        if (string.IsNullOrWhiteSpace(reason))
        {
            return currentNote ?? string.Empty;
        }

        var prefix = "Hủy hạng mục theo báo giá bổ sung: ";
        if (string.IsNullOrWhiteSpace(currentNote))
        {
            return prefix + reason;
        }

        var trimmed = currentNote.Trim();
        if (trimmed.Contains(reason, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"{trimmed} | {prefix}{reason}";
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

        if (quotation.Status == StatusCustomerApproved || quotation.Status == StatusCustomerRejected || quotation.Status == StatusCanceled)
        {
            return MapQuotationResponse(quotation);
        }

        var requiresInsuranceApproval = IsInsuranceApprovalRequiredForQuotation(quotation);
        var targetStatus = requiresInsuranceApproval ? StatusPendingInsurance : StatusInsuranceApproved;

        if (string.Equals(quotation.Status, targetStatus, StringComparison.OrdinalIgnoreCase))
        {
            return MapQuotationResponse(quotation);
        }

        if (!string.Equals(quotation.Status, StatusDraft, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chỉ có thể gửi báo giá sang trạng thái chờ bảo hiểm từ bản nháp.");
        }

        var now = VietnamTime.Now;
        quotation.Status = targetStatus;
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

        var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == quotation.ServiceOrderId);
        if (serviceOrder != null)
        {
            serviceOrder.InsuranceQuoteState = quotation.Status;
            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = actorStaffId;
        }

        await _context.SaveChangesAsync();
        return MapQuotationResponse(quotation);
    }

    public async Task<QuotationResponseDto> ConfirmInsuranceApprovedAsync(long quotationId, QuotationInsuranceApprovalRequestDto request)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự xác nhận bảo hiểm.");
        }

        var evidenceUrl = NormalizeText(request.InsuranceApprovalEvidenceUrl, 1000);
        if (string.IsNullOrWhiteSpace(evidenceUrl))
        {
            throw new InvalidOperationException("Vui lòng tải lên bằng chứng phê duyệt bảo hiểm trước khi xác nhận.");
        }

        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (string.Equals(quotation.Status, StatusInsuranceApproved, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(quotation.InsuranceApprovalEvidenceUrl))
            {
                quotation.InsuranceApprovalEvidenceUrl = evidenceUrl;
                quotation.UpdatedAt = VietnamTime.Now;
                quotation.UpdatedBy = request.ActorStaffId;
                await _context.SaveChangesAsync();
            }

            return MapQuotationResponse(quotation);
        }

        if (!string.Equals(quotation.Status, StatusPendingInsurance, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chỉ xác nhận bảo hiểm khi báo giá đang ở trạng thái chờ bảo hiểm.");
        }

        quotation.InsuranceApprovalEvidenceUrl = evidenceUrl;
        quotation.Status = StatusInsuranceApproved;
        quotation.CustomerDecisionStatus = ApprovalPending;
        var now = VietnamTime.Now;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = request.ActorStaffId;

        var cleaned = NormalizeText(request.Note, 1000);
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleaned
                : $"{quotation.InternalNotes.Trim()} | {cleaned}";
        }

        var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == quotation.ServiceOrderId);
        if (serviceOrder != null)
        {
            serviceOrder.InsuranceQuoteState = StatusInsuranceApproved;
            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = request.ActorStaffId;
        }

        await _context.SaveChangesAsync();
        return MapQuotationResponse(quotation);
    }

    public async Task<QuotationResponseDto> FinancialApproveQuotationAsync(long quotationId, QuotationFinancialApprovalRequestDto request)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        if (request == null || request.ApprovedBy <= 0)
        {
            throw new InvalidOperationException("Thiếu người duyệt tài chính.");
        }

        var quotation = await _context.Quotations
            .Include(x => x.QuotationDetails)
            .FirstOrDefaultAsync(x => x.Id == quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (!quotation.RequiresFinancialApproval)
        {
            throw new InvalidOperationException("Báo giá không yêu cầu duyệt tài chính.");
        }

        if (!string.Equals(quotation.FinancialApprovalStatus, FinancialApprovalPending, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(quotation.Status, StatusPendingBranchManager, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Báo giá không ở trạng thái chờ quản lý chi nhánh duyệt.");
        }

        var serviceOrder = await _context.ServiceOrders
            .AsNoTracking()
            .Where(x => x.Id == quotation.ServiceOrderId)
            .Select(x => new { x.ExecuteBranchId, BranchManagerId = x.ExecuteBranch.ManagerId })
            .FirstOrDefaultAsync();
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
        }

        if (serviceOrder.BranchManagerId.HasValue && serviceOrder.BranchManagerId.Value > 0 &&
            serviceOrder.BranchManagerId.Value != request.ApprovedBy)
        {
            throw new InvalidOperationException("Chỉ quản lý chi nhánh được phân quyền mới được duyệt tài chính báo giá này.");
        }

        var now = VietnamTime.Now;
        var decision = request.Approved ? FinancialApprovalApproved : FinancialApprovalRejected;
        quotation.FinancialApprovedBy = request.ApprovedBy;
        quotation.FinancialApprovedAt = now;
        quotation.FinancialApprovalStatus = decision;
        quotation.Status = request.Approved ? StatusPendingInsurance : StatusRejected;
        if (request.Approved)
        {
            quotation.RejectReason = null;
        }
        if (!request.Approved)
        {
            quotation.RejectReason = "FINANCIAL_REJECTED";
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var note = request.Note.Trim();
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? note
                : $"{quotation.InternalNotes.Trim()} | {note}";
        }

        quotation.UpdatedBy = request.ApprovedBy;
        quotation.UpdatedAt = now;

        _context.QuotationApprovalActions.Add(new QuotationApprovalAction
        {
            QuotationId = quotation.Id,
            ApprovalType = ApprovalActionTypeFinancial,
            Decision = decision,
            ActorStaffId = request.ApprovedBy,
            Note = NormalizeText(request.Note, 1000),
            CreatedAt = now
        });

        await _context.SaveChangesAsync();
        return MapQuotationResponse(quotation);
    }

    public async Task<BranchQuotationApprovalPolicyResponseDto> GetBranchQuotationApprovalPolicyAsync(long branchId)
    {
        if (branchId <= 0)
        {
            throw new InvalidOperationException("Mã chi nhánh không hợp lệ.");
        }

        var row = await _context.BranchQuotationApprovalPolicies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId);

        if (row == null)
        {
            return new BranchQuotationApprovalPolicyResponseDto
            {
                BranchId = branchId,
                MaxDiscountPercentWithoutManager = DefaultMaxDiscountPercentWithoutManager,
                HighValueThreshold = DefaultHighValueThreshold,
                IsActive = true
            };
        }

        return new BranchQuotationApprovalPolicyResponseDto
        {
            BranchId = row.BranchId,
            MaxDiscountPercentWithoutManager = row.MaxDiscountPercentWithoutManager,
            HighValueThreshold = row.HighValueThreshold,
            IsActive = row.IsActive,
            UpdatedAt = row.UpdatedAt,
            UpdatedBy = row.UpdatedBy
        };
    }

    public async Task<BranchQuotationApprovalPolicyResponseDto> UpsertBranchQuotationApprovalPolicyAsync(long branchId, BranchQuotationApprovalPolicyUpsertRequestDto request)
    {
        if (branchId <= 0)
        {
            throw new InvalidOperationException("Mã chi nhánh không hợp lệ.");
        }

        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu người cập nhật cấu hình duyệt báo giá.");
        }

        var branch = await _context.Branches.AsNoTracking()
            .Where(x => x.Id == branchId)
            .Select(x => new { x.Id, x.ManagerId })
            .FirstOrDefaultAsync();
        if (branch == null)
        {
            throw new InvalidOperationException("Không tìm thấy chi nhánh.");
        }

        if (branch.ManagerId.HasValue && branch.ManagerId.Value > 0 && branch.ManagerId.Value != request.ActorStaffId)
        {
            throw new InvalidOperationException("Chỉ quản lý chi nhánh được phân quyền mới được cập nhật cấu hình duyệt.");
        }

        var maxDiscount = request.MaxDiscountPercentWithoutManager ?? DefaultMaxDiscountPercentWithoutManager;
        var highValueThreshold = request.HighValueThreshold ?? DefaultHighValueThreshold;
        if (maxDiscount < 0m || maxDiscount > 100m)
        {
            throw new InvalidOperationException("Ngưỡng chiết khấu phải nằm trong khoảng từ 0 đến 100.");
        }

        if (highValueThreshold < 0m)
        {
            throw new InvalidOperationException("Ngưỡng giá trị báo giá phải lớn hơn hoặc bằng 0.");
        }

        var now = VietnamTime.Now;
        var entity = await _context.BranchQuotationApprovalPolicies
            .FirstOrDefaultAsync(x => x.BranchId == branchId);

        if (entity == null)
        {
            entity = new BranchQuotationApprovalPolicy
            {
                BranchId = branchId,
                MaxDiscountPercentWithoutManager = maxDiscount,
                HighValueThreshold = highValueThreshold,
                IsActive = request.IsActive,
                CreatedBy = request.ActorStaffId,
                CreatedAt = now,
                UpdatedBy = request.ActorStaffId,
                UpdatedAt = now
            };
            _context.BranchQuotationApprovalPolicies.Add(entity);
        }
        else
        {
            entity.MaxDiscountPercentWithoutManager = maxDiscount;
            entity.HighValueThreshold = highValueThreshold;
            entity.IsActive = request.IsActive;
            entity.UpdatedBy = request.ActorStaffId;
            entity.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();

        return new BranchQuotationApprovalPolicyResponseDto
        {
            BranchId = entity.BranchId,
            MaxDiscountPercentWithoutManager = entity.MaxDiscountPercentWithoutManager,
            HighValueThreshold = entity.HighValueThreshold,
            IsActive = entity.IsActive,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public async Task<QuotationResponseDto> SendQuotationToCustomerAsync(long quotationId, long actorStaffId, string? note = null, string? idempotencyKey = null)
    {
        var _ = await PublishQuotationCustomerLinkAsync(quotationId, new QuotationCustomerLinkPublishRequestDto
        {
            ActorStaffId = actorStaffId,
            Note = note,
            SendEmail = true,
            IdempotencyKey = idempotencyKey
        });

        var updated = await GetQuotationAsync(quotationId);
        if (updated == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        return updated;
    }

    public async Task<QuotationCustomerLinkResponseDto> PublishQuotationCustomerLinkAsync(long quotationId, QuotationCustomerLinkPublishRequestDto request)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự gửi báo giá cho khách.");
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
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.Status == StatusSuperseded)
        {
            throw new InvalidOperationException("Báo giá đã bị thay thế.");
        }

        if (quotation.Status == StatusCustomerApproved || quotation.Status == StatusCustomerRejected || quotation.Status == StatusCanceled)
        {
            throw new InvalidOperationException("Báo giá đã kết thúc trạng thái quyết định của khách.");
        }

        var requiresInsuranceApproval = IsInsuranceApprovalRequiredForQuotation(quotation);
        var canSendForCustomer =
            string.Equals(quotation.Status, StatusPendingCustomer, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, StatusInsuranceApproved, StringComparison.OrdinalIgnoreCase) ||
            (!requiresInsuranceApproval && string.Equals(quotation.Status, StatusDraft, StringComparison.OrdinalIgnoreCase));
        if (!canSendForCustomer)
        {
            throw new InvalidOperationException("Chỉ được tạo link cho khách sau khi bảo hiểm đã duyệt.");
        }

        if (requiresInsuranceApproval && string.IsNullOrWhiteSpace(quotation.InsuranceApprovalEvidenceUrl))
        {
            throw new InvalidOperationException("Thiếu bằng chứng duyệt bảo hiểm. Không thể tạo link cho khách.");
        }

        var cleanedNote = NormalizeText(request.Note, 1000);
        var targetEmail = NormalizeText(request.Email, 255)
            ?? NormalizeText(quotation.ServiceOrder?.Customer?.Email, 255);
        if (request.SendEmail && string.IsNullOrWhiteSpace(targetEmail))
        {
            throw new InvalidOperationException("Khách hàng chưa có email hợp lệ để gửi báo giá.");
        }

        var now = VietnamTime.Now;
        var previousStatus = quotation.Status;
        var previousInsuranceQuoteState = quotation.ServiceOrder?.InsuranceQuoteState;
        var normalizedIdempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);

        if (!string.IsNullOrWhiteSpace(normalizedIdempotencyKey))
        {
            var existingActiveLink = await _context.QuotationAccessLinks
                .Where(x => x.QuotationId == quotation.Id && x.Status == LinkStatusActive && x.ExpiresAt > now)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            if (existingActiveLink != null &&
                !string.IsNullOrWhiteSpace(existingActiveLink.ClientNote) &&
                existingActiveLink.ClientNote.Contains($"idem:{normalizedIdempotencyKey}", StringComparison.Ordinal))
            {
                var existingToken = GenerateDeterministicPublicToken(existingActiveLink);
                return new QuotationCustomerLinkResponseDto
                {
                    QuotationId = quotation.Id,
                    QuotationCode = quotation.Code,
                    Status = quotation.Status,
                    PublicLink = BuildPublicPortalUrl(existingToken),
                    ExpiresAt = existingActiveLink.ExpiresAt,
                    EmailSent = false,
                    SentToEmail = existingActiveLink.SentToEmail
                };
            }
        }

        quotation.Status = StatusPendingCustomer;
        quotation.CustomerDecisionStatus = ApprovalPending;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = request.ActorStaffId;

        if (quotation.ServiceOrder != null)
        {
            quotation.ServiceOrder.InsuranceQuoteState = StatusPendingCustomer;
            quotation.ServiceOrder.UpdatedAt = now;
            quotation.ServiceOrder.UpdatedBy = request.ActorStaffId;
        }

        if (!string.IsNullOrWhiteSpace(cleanedNote))
        {
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleanedNote
                : $"{quotation.InternalNotes.Trim()} | {cleanedNote}";
        }

        var expiresAt = ResolvePortalExpiry(quotation.ValidUntil, now);

        var existingActiveLinks = await _context.QuotationAccessLinks
            .Where(x => x.QuotationId == quotation.Id && x.Status == LinkStatusActive)
            .ToListAsync();

        foreach (var link in existingActiveLinks)
        {
            link.Status = LinkStatusRevoked;
            link.UpdatedAt = now;
            link.UpdatedBy = request.ActorStaffId;
        }

        var newLink = new QuotationAccessLink
        {
            QuotationId = quotation.Id,
            TokenHash = string.Empty,
            Status = LinkStatusActive,
            ExpiresAt = expiresAt,
            SentToEmail = targetEmail,
            ClientNote = string.IsNullOrWhiteSpace(normalizedIdempotencyKey)
                ? cleanedNote
                : string.IsNullOrWhiteSpace(cleanedNote)
                    ? $"idem:{normalizedIdempotencyKey}"
                    : $"{cleanedNote} | idem:{normalizedIdempotencyKey}",
            CreatedAt = now,
            CreatedBy = request.ActorStaffId,
            UpdatedAt = now,
            UpdatedBy = request.ActorStaffId
        };

        _context.QuotationAccessLinks.Add(newLink);
        await _context.SaveChangesAsync();

        var rawToken = GenerateDeterministicPublicToken(newLink);
        newLink.TokenHash = ComputeTokenHash(rawToken);
        newLink.UpdatedAt = VietnamTime.Now;
        newLink.UpdatedBy = request.ActorStaffId;
        await _context.SaveChangesAsync();

        var publicLink = BuildPublicPortalUrl(rawToken);

        if (request.SendEmail)
        {
            try
            {
                var emailSubject = BuildQuotationEmailSubject(quotation);
                var emailBody = BuildQuotationEmailHtml(quotation, publicLink, cleanedNote, expiresAt);
                await _emailService.SendEmailAsync(targetEmail!, emailSubject, emailBody);
            }
            catch
            {
                newLink.Status = LinkStatusRevoked;
                newLink.UpdatedAt = VietnamTime.Now;
                newLink.UpdatedBy = request.ActorStaffId;

                quotation.Status = previousStatus;
                quotation.UpdatedAt = VietnamTime.Now;
                quotation.UpdatedBy = request.ActorStaffId;

                if (quotation.ServiceOrder != null)
                {
                    quotation.ServiceOrder.InsuranceQuoteState = previousInsuranceQuoteState;
                    quotation.ServiceOrder.UpdatedAt = VietnamTime.Now;
                    quotation.ServiceOrder.UpdatedBy = request.ActorStaffId;
                }

                await _context.SaveChangesAsync();
                throw new InvalidOperationException("Không thể gửi email báo giá. Vui lòng kiểm tra cấu hình SMTP.");
            }
        }

        return new QuotationCustomerLinkResponseDto
        {
            QuotationId = quotation.Id,
            QuotationCode = quotation.Code,
            Status = quotation.Status,
            PublicLink = publicLink,
            ExpiresAt = expiresAt,
            EmailSent = request.SendEmail,
            SentToEmail = targetEmail
        };
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
        var supplementaryMetaByDetailId = await BuildSupplementaryMetaByQuotationDetailIdAsync(quotation.ServiceOrderId);
        return MapPublicQuotationResponse(quotation, supplementaryMetaByDetailId);
    }

    public async Task<QuotationPublicOtpResponseDto> SendDecisionOtpByPublicTokenAsync(string token, QuotationPublicOtpRequestDto? request = null)
    {
        var accessLink = await ResolveActiveAccessLinkAsync(token);
        var quotation = await _context.Quotations
            .Include(x => x.ServiceOrder)
                .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == accessLink.QuotationId);

        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.ServiceOrder == null)
        {
            throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
        }

        EnsureCustomerDecisionAllowed(quotation, quotation.ServiceOrder);

        if (!string.Equals(quotation.Status, StatusPendingCustomer, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Báo giá hiện không ở trạng thái chờ khách xác nhận.");
        }

        var targetEmail = NormalizeText(request?.Email, 255)
            ?? NormalizeText(accessLink.SentToEmail, 255)
            ?? NormalizeText(quotation.ServiceOrder?.Customer?.Email, 255);
        if (string.IsNullOrWhiteSpace(targetEmail))
        {
            throw new InvalidOperationException("Không tìm thấy email hợp lệ để gửi OTP.");
        }

        var now = VietnamTime.Now;
        if (accessLink.OtpLastSentAt.HasValue)
        {
            var elapsed = now - accessLink.OtpLastSentAt.Value;
            if (elapsed.TotalSeconds < OtpResendCooldownSeconds)
            {
                var retryAfter = Math.Max(1, OtpResendCooldownSeconds - (int)elapsed.TotalSeconds);
                throw new InvalidOperationException($"Vui lòng chờ {retryAfter} giây để gửi lại OTP.");
            }
        }

        var otpCode = GenerateOtpCode();
        accessLink.OtpCodeHash = ComputeTokenHash(otpCode);
        accessLink.OtpExpiresAt = now.AddSeconds(OtpTtlSeconds);
        accessLink.OtpAttemptCount = 0;
        accessLink.OtpVerifiedAt = null;
        accessLink.OtpLastSentAt = now;
        accessLink.SentToEmail = targetEmail;
        accessLink.UpdatedAt = now;
        accessLink.UpdatedBy = quotation.ServiceOrder?.CustomerId;
        await _context.SaveChangesAsync();

        try
        {
            var plate = quotation.ServiceOrder?.Vehicle?.LicensePlate?.Trim();
            var subject = string.IsNullOrWhiteSpace(plate)
                ? "MyGarage mã OTP xác nhận báo giá"
                : $"MyGarage mã OTP xác nhận báo giá xe {plate}";
            var body = BuildOtpEmailHtml(quotation, otpCode, accessLink.OtpExpiresAt.Value);
            await _emailService.SendEmailAsync(targetEmail, subject, body);
        }
        catch
        {
            accessLink.OtpCodeHash = null;
            accessLink.OtpExpiresAt = null;
            accessLink.OtpAttemptCount = 0;
            accessLink.OtpVerifiedAt = null;
            accessLink.UpdatedAt = VietnamTime.Now;
            await _context.SaveChangesAsync();
            throw new InvalidOperationException("Không thể gửi OTP. Vui lòng kiểm tra cấu hình email.");
        }

        return new QuotationPublicOtpResponseDto
        {
            Sent = true,
            MaskedEmail = MaskEmail(targetEmail),
            OtpExpiresAt = accessLink.OtpExpiresAt.Value,
            RetryAfterSeconds = OtpResendCooldownSeconds
        };
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

        EnsureCustomerDecisionAllowed(quotation, serviceOrder);

        await ValidateOtpForDecisionAsync(accessLink, request.OtpCode);

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
        accessLink.OtpVerifiedAt = now;
        accessLink.OtpCodeHash = null;
        accessLink.OtpExpiresAt = null;
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

        if (quotation.IsAppliedToServiceOrder || quotation.Status == StatusInsuranceApproved)
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

    private async Task TryNotifyInsuranceApprovalChainAsync(Quotation quotation, ServiceOrder serviceOrder, bool approved)
    {
        string subject = string.Empty;
        string content = string.Empty;
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var statusText = approved ? "đã duyệt báo giá" : "đã từ chối báo giá";
            var vehiclePlate = serviceOrder.Vehicle?.LicensePlate?.Trim() ?? "-";
            var serviceOrderCode = serviceOrder.Code?.Trim() ?? serviceOrder.Id.ToString(CultureInfo.InvariantCulture);
            subject = approved
                ? $"Khách đã duyệt báo giá - {vehiclePlate}"
                : $"Khách từ chối báo giá - {vehiclePlate}";

            content = $@"
<div style=""font-family:Arial,sans-serif;color:#1a202c;line-height:1.5;"">
  <h3 style=""margin:0 0 10px;color:#1a365d;"">Thông báo tự động từ MyGarage</h3>
  <p>Khách xe <strong>{WebUtility.HtmlEncode(vehiclePlate)}</strong> {statusText}.</p>
  <p>Lệnh sửa chữa: <strong>{WebUtility.HtmlEncode(serviceOrderCode)}</strong><br/>
     Báo giá: <strong>{WebUtility.HtmlEncode(quotation.Code)}</strong></p>
  <p>Trạng thái hiện tại: <strong>{WebUtility.HtmlEncode(quotation.Status)}</strong></p>
</div>";

            var serviceAdvisorEmail = await _context.Staffs.AsNoTracking()
                .Where(x => x.Id == serviceOrder.ServiceAdvisorId)
                .Select(x => x.Email)
                .FirstOrDefaultAsync();

            var roleCodes = new[] { "WORKSHOP_MANAGER", "FOREMAN", "TEAM_LEADER", "STOREKEEPER" };
            var fallbackRoleHints = new[] { "quan doc", "to truong", "thu kho", "manager", "foreman", "leader", "storekeeper" };

            var branchStaffCandidates = await (
                from staff in _context.Staffs.AsNoTracking()
                join role in _context.Roles.AsNoTracking() on staff.RoleId equals role.Id
                where staff.BranchId == serviceOrder.ExecuteBranchId
                select new
                {
                    staff.Id,
                    staff.Email,
                    RoleCode = role.RoleCode,
                    RoleName = role.RoleName
                }
            ).ToListAsync();

            var branchStaffEmails = branchStaffCandidates
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.Email) &&
                    (
                        roleCodes.Contains((x.RoleCode ?? string.Empty).Trim().ToUpperInvariant()) ||
                        fallbackRoleHints.Any(h =>
                            (x.RoleName ?? string.Empty).Trim().ToLowerInvariant().Contains(h) ||
                            (x.RoleCode ?? string.Empty).Trim().ToLowerInvariant().Contains(h))
                    ))
                .Select(x => x.Email!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(serviceAdvisorEmail))
            {
                recipients.Add(serviceAdvisorEmail.Trim());
            }

            foreach (var email in branchStaffEmails.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                recipients.Add(email.Trim());
            }

            foreach (var recipient in recipients)
            {
                await _emailService.SendEmailAsync(recipient, subject, content);
            }

            var payload = new
            {
                eventType = "QUOTATION_CUSTOMER_DECISION",
                approved,
                quotationId = quotation.Id,
                quotationCode = quotation.Code,
                serviceOrderId = serviceOrder.Id,
                serviceOrderCode = serviceOrder.Code,
                vehiclePlate,
                decidedAt = quotation.CustomerDecisionAt
            };

            if (serviceOrder.ExecuteBranchId > 0)
            {
                await _chatHubContext.Clients
                    .Group($"WorkshopBranch_{serviceOrder.ExecuteBranchId}")
                    .SendAsync("QuotationWorkflowUpdated", payload);

                await _chatHubContext.Clients
                    .Group($"WorkshopBranch_{serviceOrder.ExecuteBranchId}")
                    .SendAsync("ServiceOrderExecutionReady", new
                    {
                        serviceOrderId = serviceOrder.Id,
                        serviceOrderCode = serviceOrder.Code,
                        quotationId = quotation.Id,
                        quotationCode = quotation.Code,
                        approved,
                        message = approved
                            ? "Khách hàng đã duyệt báo giá, sẵn sàng phát lệnh thi công."
                            : "Khách hàng đã từ chối báo giá, cần xử lý lại phương án."
                    });
            }

            if (serviceOrder.ServiceAdvisorId.HasValue && serviceOrder.ServiceAdvisorId.Value > 0)
            {
                await _chatHubContext.Clients
                    .Group($"User_{serviceOrder.ServiceAdvisorId.Value}")
                    .SendAsync("QuotationWorkflowUpdated", payload);

                await _chatHubContext.Clients
                    .Group($"User_{serviceOrder.ServiceAdvisorId.Value}")
                    .SendAsync("ServiceAdvisorNotification", new
                    {
                        serviceOrderId = serviceOrder.Id,
                        quotationId = quotation.Id,
                        approved,
                        message = approved
                            ? "Khách đã duyệt báo giá."
                            : "Khách đã từ chối báo giá."
                    });
            }

            var foremanIds = branchStaffCandidates
                .Where(x =>
                    (x.RoleCode ?? string.Empty).Contains("FOREMAN", StringComparison.OrdinalIgnoreCase) ||
                    (x.RoleCode ?? string.Empty).Contains("TEAM_LEADER", StringComparison.OrdinalIgnoreCase) ||
                    (x.RoleName ?? string.Empty).Contains("quản đốc", StringComparison.OrdinalIgnoreCase) ||
                    (x.RoleName ?? string.Empty).Contains("tổ trưởng", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            var storekeeperIds = branchStaffCandidates
                .Where(x =>
                    (x.RoleCode ?? string.Empty).Contains("STOREKEEPER", StringComparison.OrdinalIgnoreCase) ||
                    (x.RoleCode ?? string.Empty).Contains("WAREHOUSE", StringComparison.OrdinalIgnoreCase) ||
                    (x.RoleName ?? string.Empty).Contains("thủ kho", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            foreach (var staffId in foremanIds)
            {
                await _chatHubContext.Clients
                    .Group($"User_{staffId}")
                    .SendAsync("ForemanDispatchNotification", new
                    {
                        serviceOrderId = serviceOrder.Id,
                        quotationId = quotation.Id,
                        approved,
                        message = approved
                            ? "Báo giá đã được khách duyệt, điều phối xe vào khoang thi công."
                            : "Báo giá bị từ chối, tạm dừng điều phối thi công."
                    });
            }

            foreach (var staffId in storekeeperIds)
            {
                await _chatHubContext.Clients
                    .Group($"User_{staffId}")
                    .SendAsync("StorekeeperDispatchNotification", new
                    {
                        serviceOrderId = serviceOrder.Id,
                        quotationId = quotation.Id,
                        approved,
                        message = approved
                            ? "Báo giá đã duyệt, chuẩn bị xuất đúng vật tư theo lệnh."
                            : "Báo giá bị từ chối, giữ nguyên kế hoạch xuất kho."
                    });
            }
        }
        catch
        {
            await EnqueueNotificationRetryAsync(
                quotation,
                serviceOrder,
                subject,
                content,
                recipients.ToList());
        }
    }

    private async Task EnqueueNotificationRetryAsync(
        Quotation quotation,
        ServiceOrder serviceOrder,
        string subject,
        string content,
        IReadOnlyList<string> recipients)
    {
        try
        {
            var sanitizedRecipients = recipients
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (sanitizedRecipients.Count == 0)
            {
                return;
            }

            var payload = JsonSerializer.Serialize(new
            {
                subject,
                content,
                recipients = sanitizedRecipients,
                retryCount = 0,
                maxRetries = 5,
                quotationId = quotation.Id,
                serviceOrderId = serviceOrder.Id
            });

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = quotation.UpdatedBy ?? quotation.CreatedBy ?? 0,
                UserType = "SYSTEM",
                BranchId = serviceOrder.ExecuteBranchId,
                Action = "NOTIFY_RETRY_PENDING",
                TableName = "QUOTATIONS",
                RecordId = quotation.Id,
                NewValue = payload,
                Timestamp = VietnamTime.Now
            });

            await _context.SaveChangesAsync();
        }
        catch
        {
            // Do not block workflow when retry queue logging fails.
        }
    }

    private static string BuildQuotationCode(long serviceOrderId, int versionNo, DateTime now)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"QT-{now:yyyyMMddHHmmss}-{serviceOrderId}-{versionNo:D3}");
    }

    private static long? ResolveServiceAdvisorForQuotation(
        long? requestedServiceAdvisorId,
        long? serviceOrderServiceAdvisorId,
        long? serviceOrderReceptionistId)
    {
        if (requestedServiceAdvisorId.HasValue && requestedServiceAdvisorId.Value > 0)
        {
            return requestedServiceAdvisorId.Value;
        }

        if (serviceOrderServiceAdvisorId.HasValue && serviceOrderServiceAdvisorId.Value > 0)
        {
            return serviceOrderServiceAdvisorId.Value;
        }

        if (serviceOrderReceptionistId.HasValue && serviceOrderReceptionistId.Value > 0)
        {
            return serviceOrderReceptionistId.Value;
        }

        return null;
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

    private static string NormalizeQuotationDetailStatus(string? rawValue)
    {
        var normalized = (rawValue ?? string.Empty).Trim().ToUpperInvariant();
        return normalized == DetailStatusCanceled ? DetailStatusCanceled : DetailStatusActive;
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

    private static string? NormalizeIdempotencyKey(string? key)
    {
        var normalized = (key ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > 120)
        {
            normalized = normalized[..120];
        }

        return normalized;
    }

    private static string GenerateDeterministicPublicToken(QuotationAccessLink link)
    {
        var payload = $"{link.QuotationId}:{link.Id}:{link.CreatedAt.Ticks}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
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

    private static string GenerateOtpCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString($"D{OtpLength}", CultureInfo.InvariantCulture);
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

    private static string BuildOtpEmailHtml(Quotation quotation, string otpCode, DateTime expiresAt)
    {
        var customer = WebUtility.HtmlEncode(quotation.ServiceOrder?.Customer?.FullName?.Trim() ?? "Quý khách");
        var plate = WebUtility.HtmlEncode(quotation.ServiceOrder?.Vehicle?.LicensePlate?.Trim() ?? "-");
        var expiryText = expiresAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN"));
        var amount = quotation.TotalCustomerAmount.ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN"));

        return $@"
<div style=""font-family:Arial,sans-serif;max-width:680px;margin:0 auto;color:#1a202c;line-height:1.5;"">
  <h2 style=""margin:0 0 12px;color:#1a365d;"">MyGarage - Xác nhận duyệt báo giá</h2>
  <p>Kính chào anh/chị <strong>{customer}</strong>, vui lòng nhập mã OTP bên dưới để xác nhận báo giá cho xe <strong>{plate}</strong>.</p>
  <div style=""margin:14px 0;padding:14px 16px;background:#f0f7ff;border:1px solid #c6dcf8;border-radius:10px;"">
    <p style=""margin:0 0 6px;color:#31577c;font-size:13px;"">Mã OTP</p>
    <p style=""margin:0;font-size:30px;font-weight:800;letter-spacing:8px;color:#0f3b67;"">{WebUtility.HtmlEncode(otpCode)}</p>
  </div>
  <p style=""margin:8px 0;color:#4a5568;"">Tổng phần khách cần thanh toán: <strong>{amount} đ</strong></p>
  <p style=""margin:8px 0;color:#4a5568;"">OTP hết hạn lúc: <strong>{expiryText}</strong></p>
  <p style=""margin:14px 0 0;color:#4a5568;"">Nếu không thực hiện thao tác này, vui lòng bỏ qua email.</p>
</div>";
    }

    private static string MaskEmail(string email)
    {
        var normalized = (email ?? string.Empty).Trim();
        var at = normalized.IndexOf('@');
        if (at <= 1)
        {
            return normalized;
        }

        var local = normalized[..at];
        var domain = normalized[(at + 1)..];
        var visible = Math.Min(2, local.Length);
        var masked = $"{local[..visible]}{new string('*', Math.Max(1, local.Length - visible))}";
        return $"{masked}@{domain}";
    }

    private async Task ValidateOtpForDecisionAsync(QuotationAccessLink accessLink, string? rawOtpCode)
    {
        var otpCode = (rawOtpCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(otpCode))
        {
            throw new InvalidOperationException("Vui lòng nhập mã OTP trước khi xác nhận.");
        }

        if (string.IsNullOrWhiteSpace(accessLink.OtpCodeHash) || !accessLink.OtpExpiresAt.HasValue)
        {
            throw new InvalidOperationException("Vui lòng bấm gửi OTP để xác thực trước khi duyệt báo giá.");
        }

        var now = VietnamTime.Now;
        if (accessLink.OtpExpiresAt.Value <= now)
        {
            throw new InvalidOperationException("Mã OTP đã hết hạn. Vui lòng gửi lại OTP mới.");
        }

        if (accessLink.OtpAttemptCount >= OtpMaxAttempts)
        {
            if (!string.Equals(accessLink.Status, LinkStatusRevoked, StringComparison.OrdinalIgnoreCase))
            {
                accessLink.Status = LinkStatusRevoked;
                accessLink.OtpCodeHash = null;
                accessLink.OtpExpiresAt = null;
                accessLink.OtpVerifiedAt = null;
                accessLink.UpdatedAt = now;
                await _context.SaveChangesAsync();
            }
            throw new InvalidOperationException("OTP đã nhập sai quá số lần cho phép. Liên kết đã bị khóa, vui lòng liên hệ CVDV.");
        }

        var otpHash = ComputeTokenHash(otpCode);
        if (!string.Equals(otpHash, accessLink.OtpCodeHash, StringComparison.OrdinalIgnoreCase))
        {
            accessLink.OtpAttemptCount += 1;
            if (accessLink.OtpAttemptCount >= OtpMaxAttempts)
            {
                accessLink.Status = LinkStatusRevoked;
                accessLink.OtpCodeHash = null;
                accessLink.OtpExpiresAt = null;
                accessLink.OtpVerifiedAt = null;
                accessLink.UpdatedAt = now;
                await _context.SaveChangesAsync();
                throw new InvalidOperationException("OTP đã nhập sai quá số lần cho phép. Liên kết đã bị khóa, vui lòng liên hệ CVDV.");
            }

            accessLink.UpdatedAt = now;
            await _context.SaveChangesAsync();
            throw new InvalidOperationException("Mã OTP không đúng.");
        }
    }

    private static bool IsCanceledState(string? status)
    {
        var normalized = (status ?? string.Empty).Trim().ToUpperInvariant();
        return normalized == StatusCanceled || normalized == LegacyStatusCancelled;
    }

    private static void EnsureCustomerDecisionAllowed(Quotation quotation, ServiceOrder serviceOrder)
    {
        if (IsCanceledState(quotation.Status))
        {
            throw new InvalidOperationException("Báo giá đã hủy, không thể duyệt từ liên kết.");
        }

        if (IsCanceledState(serviceOrder.Status))
        {
            throw new InvalidOperationException("Lệnh sửa chữa đã hủy, không thể duyệt báo giá từ liên kết.");
        }
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

    private static QuotationPublicViewResponseDto MapPublicQuotationResponse(
        Quotation quotation,
        IReadOnlyDictionary<long, QuotationDetailSupplementaryMeta>? supplementaryMetaByDetailId = null)
    {
        var status = quotation.Status ?? StatusDraft;
        var canAct = string.Equals(status, StatusPendingCustomer, StringComparison.OrdinalIgnoreCase);

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
                .Select(x =>
                {
                    var supplementaryMeta = ResolveQuotationDetailSupplementaryMeta(quotation, x, supplementaryMetaByDetailId);
                    return new QuotationPublicDetailResponseDto
                    {
                        SourceQuotationId = supplementaryMeta.SourceQuotationId,
                        ServiceSource = supplementaryMeta.ServiceSource,
                        IsSupplementary = supplementaryMeta.IsSupplementary,
                        SupplementaryReasonCode = supplementaryMeta.SupplementaryReasonCode,
                        Id = x.Id,
                        ItemType = x.ItemType,
                        ItemName = x.ItemName,
                        Quantity = x.Quantity,
                        UnitPriceAtTime = x.UnitPriceAtTime,
                        FinalAmount = x.FinalAmount,
                        IsMandatory = x.IsMandatory,
                        Status = string.IsNullOrWhiteSpace(x.Status) ? DetailStatusActive : x.Status,
                        CancelReason = x.CancelReason
                    };
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
        var split = CalculateSplitBilling(finalAmount, dto.InsuranceCoveredAmount, dto.CustomerCoveredAmount, normalizedPayerType);
        var normalizedDetailStatus = NormalizeQuotationDetailStatus(dto.Status);
        var cancelReason = normalizedDetailStatus == DetailStatusCanceled
            ? NormalizeText(dto.CancelReason, 500) ?? "Khách yêu cầu hủy hạng mục"
            : null;
        var canceledBy = normalizedDetailStatus == DetailStatusCanceled
            ? (dto.CanceledBy ?? actorStaffId)
            : null;

        return new QuotationDetail
        {
            ServiceOrderId = serviceOrderId,
            SourceQuotationDetailId = dto.SourceQuotationDetailId,
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
            ApprovalStatus = ApprovalPending,
            Status = normalizedDetailStatus,
            CancelReason = cancelReason,
            CanceledBy = canceledBy,
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

    private static bool IsInsuranceApprovalRequiredForQuotation(Quotation quotation)
    {
        if (quotation?.QuotationDetails == null || quotation.QuotationDetails.Count == 0)
        {
            return false;
        }

        return quotation.QuotationDetails.Any(IsInsuranceResponsibleDetail);
    }

    private async Task<bool> IsFinancialApprovalRequiredAsync(Quotation quotation)
    {
        if (quotation == null || quotation.ServiceOrderId <= 0)
        {
            return false;
        }

        var branchId = await _context.ServiceOrders.AsNoTracking()
            .Where(x => x.Id == quotation.ServiceOrderId)
            .Select(x => (long?)x.ExecuteBranchId)
            .FirstOrDefaultAsync();
        if (!branchId.HasValue || branchId.Value <= 0)
        {
            return false;
        }

        var policy = await _context.BranchQuotationApprovalPolicies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId.Value && x.IsActive);
        var maxDiscountPercentWithoutManager = policy?.MaxDiscountPercentWithoutManager ?? DefaultMaxDiscountPercentWithoutManager;
        var highValueThreshold = policy?.HighValueThreshold ?? DefaultHighValueThreshold;

        var subtotal = Math.Max(0m, quotation.SubTotalAmount);
        var totalDiscount = Math.Max(0m, quotation.TotalDiscount);
        var discountPercent = subtotal <= 0m
            ? 0m
            : Math.Round((totalDiscount / subtotal) * 100m, 2, MidpointRounding.AwayFromZero);

        if (discountPercent > maxDiscountPercentWithoutManager)
        {
            return true;
        }

        if (highValueThreshold > 0m && quotation.FinalAmount >= highValueThreshold)
        {
            return true;
        }

        return false;
    }

    private static bool IsInsuranceResponsibleDetail(QuotationDetail detail)
    {
        if (detail == null || string.Equals(detail.Status, DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ResolveInsuranceCoveredAmount(detail) > 0m;
    }

    private static decimal ResolveInsuranceCoveredAmount(QuotationDetail detail)
    {
        if (detail == null)
        {
            return 0m;
        }

        if (detail.InsuranceCoveredAmount.HasValue)
        {
            return Math.Max(0m, detail.InsuranceCoveredAmount.Value);
        }

        var payerType = (detail.PayerType ?? string.Empty).Trim().ToUpperInvariant();
        return payerType is "INSURANCE" or "SPLIT"
            ? Math.Max(0m, detail.FinalAmount)
            : 0m;
    }

    private async Task<string> ResolveInsuranceChangeTypeAsync(Quotation quotation, decimal? previousInsuranceAmountOverride)
    {
        if (quotation == null)
        {
            return InsuranceChangeTypeNone;
        }

        var currentInsuranceAmount = Math.Max(0m, quotation.TotalInsuranceAmount);
        decimal? previousInsuranceAmount = previousInsuranceAmountOverride;
        if (!previousInsuranceAmount.HasValue)
        {
            var previousVersionNo = quotation.VersionNo - 1;
            if (previousVersionNo > 0)
            {
                previousInsuranceAmount = await _context.Quotations.AsNoTracking()
                    .Where(x => x.ServiceOrderId == quotation.ServiceOrderId && x.VersionNo == previousVersionNo)
                    .Select(x => (decimal?)x.TotalInsuranceAmount)
                    .FirstOrDefaultAsync();
            }
        }

        if (!previousInsuranceAmount.HasValue)
        {
            return currentInsuranceAmount > 0m ? InsuranceChangeTypeIncrease : InsuranceChangeTypeNone;
        }

        var previous = Math.Max(0m, previousInsuranceAmount.Value);
        if (currentInsuranceAmount > previous)
        {
            return InsuranceChangeTypeIncrease;
        }

        if (currentInsuranceAmount < previous)
        {
            return InsuranceChangeTypeDecrease;
        }

        return InsuranceChangeTypeNone;
    }

    private static string NormalizeInsuranceChangeType(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            InsuranceChangeTypeIncrease => InsuranceChangeTypeIncrease,
            InsuranceChangeTypeDecrease => InsuranceChangeTypeDecrease,
            _ => InsuranceChangeTypeNone
        };
    }

    private async Task<bool> IsTechnicianSupplementaryQuotationAsync(Quotation quotation, long serviceOrderId)
    {
        if (quotation.VersionNo <= 1)
        {
            return false;
        }

        var initialQuotation = await _context.Quotations.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .OrderBy(x => x.VersionNo)
            .ThenBy(x => x.CreatedAt ?? DateTime.MinValue)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.DiagnosisAssignmentId
            })
            .FirstOrDefaultAsync();

        if (initialQuotation == null ||
            initialQuotation.Id == quotation.Id ||
            !initialQuotation.DiagnosisAssignmentId.HasValue ||
            !quotation.DiagnosisAssignmentId.HasValue)
        {
            return false;
        }

        return initialQuotation.DiagnosisAssignmentId.Value != quotation.DiagnosisAssignmentId.Value;
    }

    private static ServiceOrderDetail BuildServiceOrderDetailFromQuotation(
        QuotationDetail detail,
        Quotation quotation,
        long serviceOrderId,
        long actorStaffId,
        DateTime now,
        bool isTechnicianSupplementary)
    {
        var normalizedItemType = NormalizeServiceOrderDetailItemType(detail.ItemType);
        var normalizedPayerType = NormalizeServiceOrderDetailPayerType(
            detail.PayerType,
            detail.InsuranceCoveredAmount,
            detail.CustomerCoveredAmount);
        var isSupplementary = quotation.VersionNo > 1;
        var supplementaryReason = ResolveSupplementaryReasonCode(isSupplementary, isTechnicianSupplementary);
        var serviceSource = ResolveServiceSourceCode(isSupplementary, supplementaryReason);
        var itemId = detail.ItemId;
        var standardServiceId = normalizedItemType == "LABOR" && itemId.HasValue && itemId.Value > 0
            ? itemId
            : null;

        return new ServiceOrderDetail
        {
            ServiceOrderId = serviceOrderId,
            QuotationDetailId = detail.Id,
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
            PayerType = normalizedPayerType,
            InsuranceCompanyId = detail.InsuranceCompanyId,
            InsuranceRequestedAmount = detail.InsuranceCoveredAmount,
            CustomerCoPayAmount = detail.CustomerCoveredAmount,
            Status = nameof(ServiceOrderDetailStatusCode.APPROVED),
            IsReturnOldPart = detail.IsReturnOldPart,
            ServiceSource = serviceSource,
            IsSupplementary = isSupplementary,
            SupplementaryReasonCode = supplementaryReason,
            SourceQuotationId = quotation.Id,
            SupplementaryRequestedByStaffId = isSupplementary ? (quotation.CreatedBy ?? actorStaffId) : null,
            SupplementaryRequestedAt = isSupplementary ? (quotation.CreatedAt ?? now) : null,
            CustomerApprovedAt = isSupplementary ? now : null,
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

    private static string NormalizeServiceOrderDetailPayerType(string? payerType, decimal? insuranceAmount, decimal? customerAmount)
    {
        var normalized = (payerType ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized == "INSURANCE" || normalized == "CUSTOMER")
        {
            return normalized;
        }

        // ServiceOrderDetails currently supports only INSURANCE/CUSTOMER.
        // For split rows, keep accounting split amounts and mark payer by dominant coverage side.
        var insurance = insuranceAmount ?? 0m;
        var customer = customerAmount ?? 0m;
        return insurance > 0m && insurance >= customer ? "INSURANCE" : "CUSTOMER";
    }

    private static bool IsMandatoryByUrgency(string? urgency)
    {
        var normalized = (urgency ?? string.Empty).Trim().ToUpperInvariant();
        return normalized is "CRITICAL" or "SAFETY" or "SAFETY_CRITICAL" or "MANDATORY";
    }

    private static (string ItemType, long? ItemId, string ItemName, decimal UnitPrice, decimal? CostPrice, decimal TaxRate) MapRecommendationToPrice(
        DiagnosisRecommendation recommendation,
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
        var details = (quotation.QuotationDetails ?? new List<QuotationDetail>())
            .Where(x => !string.Equals(x.Status, DetailStatusCanceled, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var detail in details)
        {
            var quantity = detail.Quantity <= 0 ? 1m : detail.Quantity;
            var unitPrice = detail.UnitPriceAtTime < 0 ? 0m : detail.UnitPriceAtTime;
            var discount = detail.DiscountValue < 0 ? 0m : detail.DiscountValue;
            var taxRate = detail.TaxRate < 0 ? 0m : detail.TaxRate;

            var subTotal = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
            var taxableBase = Math.Max(0m, subTotal - discount);
            var taxAmount = Math.Round(taxableBase * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
            var finalAmount = Math.Round(Math.Max(0m, taxableBase + taxAmount), 2, MidpointRounding.AwayFromZero);
            var split = CalculateSplitBilling(finalAmount, detail.InsuranceCoveredAmount, detail.CustomerCoveredAmount, detail.PayerType);

            detail.Quantity = quantity;
            detail.UnitPriceAtTime = unitPrice;
            detail.DiscountValue = discount;
            detail.TaxRate = taxRate;
            detail.SubTotal = subTotal;
            detail.TaxAmount = taxAmount;
            detail.FinalAmount = finalAmount;
            detail.PayerType = split.PayerType;
            detail.InsuranceCoveredAmount = split.InsuranceCoveredAmount;
            detail.CustomerCoveredAmount = split.CustomerCoveredAmount;
        }

        quotation.SubTotalAmount = details.Sum(x => x.SubTotal);
        quotation.TotalDiscount = details.Sum(x => x.DiscountValue);
        quotation.TaxAmount = details.Sum(x => x.TaxAmount);
        quotation.FinalAmount = details.Sum(x => x.FinalAmount);
        quotation.TotalInsuranceAmount = details.Sum(x =>
            x.InsuranceCoveredAmount
            ?? (string.Equals(x.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase) ? x.FinalAmount : 0m));
        quotation.TotalCustomerAmount = details.Sum(x =>
            x.CustomerCoveredAmount
            ?? (!string.Equals(x.PayerType, "INSURANCE", StringComparison.OrdinalIgnoreCase)
                ? x.FinalAmount
                : Math.Max(0m, x.FinalAmount - (x.InsuranceCoveredAmount ?? 0m))));
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
            .Where(x => !string.Equals(x.Status, nameof(ServiceOrderDetailStatusCode.CANCELED), StringComparison.OrdinalIgnoreCase))
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

    private sealed class QuotationDetailSupplementaryMeta
    {
        public bool IsSupplementary { get; init; }
        public string ServiceSource { get; init; } = nameof(ServiceOrderDetailServiceSourceCode.INITIAL);
        public string? SupplementaryReasonCode { get; init; }
        public long? SourceQuotationId { get; init; }
    }

    private async Task<Dictionary<long, QuotationDetailSupplementaryMeta>> BuildSupplementaryMetaByQuotationDetailIdAsync(long serviceOrderId)
    {
        var result = new Dictionary<long, QuotationDetailSupplementaryMeta>();
        if (serviceOrderId <= 0)
        {
            return result;
        }

        var sodRows = await _context.ServiceOrderDetails.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Where(x => x.QuotationDetailId.HasValue && x.QuotationDetailId.Value > 0)
            .Select(x => new
            {
                QuotationDetailId = x.QuotationDetailId!.Value,
                x.IsSupplementary,
                x.ServiceSource,
                x.SupplementaryReasonCode,
                x.SourceQuotationId,
                x.UpdatedAt,
                x.Id
            })
            .ToListAsync();

        foreach (var row in sodRows
                     .OrderByDescending(x => x.UpdatedAt ?? DateTime.MinValue)
                     .ThenByDescending(x => x.Id))
        {
            if (result.ContainsKey(row.QuotationDetailId))
            {
                continue;
            }

            var normalizedSource = NormalizeServiceSourceForResponse(row.ServiceSource);
            var normalizedReason = NormalizeSupplementaryReasonForResponse(row.SupplementaryReasonCode);
            var isSupplementary = row.IsSupplementary ||
                                  string.Equals(normalizedSource, nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN), StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(normalizedSource, nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER), StringComparison.OrdinalIgnoreCase);

            if (!isSupplementary)
            {
                normalizedSource = nameof(ServiceOrderDetailServiceSourceCode.INITIAL);
                normalizedReason = null;
            }

            result[row.QuotationDetailId] = new QuotationDetailSupplementaryMeta
            {
                IsSupplementary = isSupplementary,
                ServiceSource = normalizedSource,
                SupplementaryReasonCode = normalizedReason,
                SourceQuotationId = isSupplementary ? row.SourceQuotationId : null
            };
        }

        var quotationRows = await _context.Quotations.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Select(x => new
            {
                x.Id,
                x.VersionNo,
                x.DiagnosisAssignmentId
            })
            .ToListAsync();
        var versionByQuotationId = quotationRows.ToDictionary(x => x.Id, x => x.VersionNo);
        var diagnosisAssignmentIdByQuotationId = quotationRows.ToDictionary(x => x.Id, x => x.DiagnosisAssignmentId);
        var initialDiagnosisAssignmentId = quotationRows
            .OrderBy(x => x.VersionNo)
            .ThenBy(x => x.Id)
            .Select(x => x.DiagnosisAssignmentId)
            .FirstOrDefault();

        var quotationDetailRows = await _context.QuotationDetails.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Select(x => new
            {
                x.Id,
                x.QuotationId,
                x.SourceQuotationDetailId
            })
            .ToListAsync();
        var quotationDetailById = quotationDetailRows.ToDictionary(x => x.Id, x => x);

        QuotationDetailSupplementaryMeta ResolveFromLineage(long quotationDetailId)
        {
            var visited = new HashSet<long>();
            var cursorId = quotationDetailId;
            long? rootQuotationId = null;
            var rootVersionNo = 1;

            while (cursorId > 0 && visited.Add(cursorId))
            {
                if (result.TryGetValue(cursorId, out var existingMeta))
                {
                    return existingMeta;
                }

                if (!quotationDetailById.TryGetValue(cursorId, out var detailNode))
                {
                    break;
                }

                rootQuotationId = detailNode.QuotationId;
                if (versionByQuotationId.TryGetValue(detailNode.QuotationId, out var mappedVersionNo))
                {
                    rootVersionNo = mappedVersionNo;
                }

                if (!detailNode.SourceQuotationDetailId.HasValue || detailNode.SourceQuotationDetailId.Value <= 0)
                {
                    break;
                }

                cursorId = detailNode.SourceQuotationDetailId.Value;
            }

            var isSupplementary = rootVersionNo > 1;
            var isTechnicianSupplementary = isSupplementary &&
                rootQuotationId.HasValue &&
                diagnosisAssignmentIdByQuotationId.TryGetValue(rootQuotationId.Value, out var rootDiagnosisAssignmentId) &&
                rootDiagnosisAssignmentId.HasValue &&
                initialDiagnosisAssignmentId.HasValue &&
                rootDiagnosisAssignmentId.Value != initialDiagnosisAssignmentId.Value;
            var supplementaryReasonCode = ResolveSupplementaryReasonCode(isSupplementary, isTechnicianSupplementary);
            var serviceSource = ResolveServiceSourceCode(isSupplementary, supplementaryReasonCode);

            return new QuotationDetailSupplementaryMeta
            {
                IsSupplementary = isSupplementary,
                ServiceSource = serviceSource,
                SupplementaryReasonCode = supplementaryReasonCode,
                SourceQuotationId = isSupplementary ? rootQuotationId : null
            };
        }

        foreach (var row in quotationDetailRows)
        {
            if (result.ContainsKey(row.Id))
            {
                continue;
            }

            result[row.Id] = ResolveFromLineage(row.Id);
        }

        return result;
    }

    private static string NormalizeServiceSourceForResponse(string? source)
    {
        var normalized = (source ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN) => nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN),
            nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER) => nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER),
            _ => nameof(ServiceOrderDetailServiceSourceCode.INITIAL)
        };
    }

    private static string? ResolveSupplementaryReasonCode(bool isSupplementary, bool isTechnicianSupplementary)
    {
        if (!isSupplementary)
        {
            return null;
        }

        return isTechnicianSupplementary
            ? nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING)
            : nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST);
    }

    private static string ResolveServiceSourceCode(bool isSupplementary, string? supplementaryReasonCode)
    {
        if (!isSupplementary)
        {
            return nameof(ServiceOrderDetailServiceSourceCode.INITIAL);
        }

        return string.Equals(
            supplementaryReasonCode,
            nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING),
            StringComparison.OrdinalIgnoreCase)
            ? nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN)
            : nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER);
    }

    private static string? NormalizeSupplementaryReasonForResponse(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING) => nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING),
            nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST) => nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST),
            _ => null
        };
    }

    private static QuotationDetailSupplementaryMeta ResolveQuotationDetailSupplementaryMeta(
        Quotation quotation,
        QuotationDetail detail,
        IReadOnlyDictionary<long, QuotationDetailSupplementaryMeta>? supplementaryMetaByDetailId)
    {
        if (supplementaryMetaByDetailId != null &&
            supplementaryMetaByDetailId.TryGetValue(detail.Id, out var metaFromMap))
        {
            return metaFromMap;
        }

        var isSupplementary = quotation.VersionNo > 1;
        var supplementaryReasonCode = ResolveSupplementaryReasonCode(isSupplementary, false);
        var serviceSource = ResolveServiceSourceCode(isSupplementary, supplementaryReasonCode);

        return new QuotationDetailSupplementaryMeta
        {
            IsSupplementary = isSupplementary,
            ServiceSource = serviceSource,
            SupplementaryReasonCode = supplementaryReasonCode,
            SourceQuotationId = isSupplementary ? quotation.Id : null
        };
    }

    private static QuotationResponseDto MapQuotationResponse(
        Quotation quotation,
        IReadOnlyCollection<string>? warnings = null,
        IReadOnlyDictionary<long, QuotationDetailSupplementaryMeta>? supplementaryMetaByDetailId = null)
    {
        return new QuotationResponseDto
        {
            Id = quotation.Id,
            Code = quotation.Code,
            ServiceOrderId = quotation.ServiceOrderId,
            DiagnosisId = quotation.DiagnosisId,
            DiagnosisAssignmentId = quotation.DiagnosisAssignmentId,
            SourceSnapshotId = quotation.SourceSnapshotId,
            Title = quotation.Title,
            QuotationType = quotation.QuotationType,
            VersionNo = quotation.VersionNo,
            Status = quotation.Status,
            InsuranceClaimNumber = quotation.InsuranceClaimNumber,
            InsuranceApprovalEvidenceUrl = quotation.InsuranceApprovalEvidenceUrl,
            TechnicalApprovedBy = quotation.TechnicalApprovedBy,
            TechnicalApprovedAt = quotation.TechnicalApprovedAt,
            TechnicalApprovalStatus = quotation.TechnicalApprovalStatus,
            RequiresFinancialApproval = quotation.RequiresFinancialApproval,
            FinancialApprovedBy = quotation.FinancialApprovedBy,
            FinancialApprovedAt = quotation.FinancialApprovedAt,
            FinancialApprovalStatus = quotation.FinancialApprovalStatus,
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
            InsuranceChangeType = NormalizeInsuranceChangeType(quotation.InsuranceChangeType),
            Warnings = warnings?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList() ?? new List<string>(),
            IsAppliedToServiceOrder = quotation.IsAppliedToServiceOrder,
            IsReadyForSplitBilling = quotation.IsReadyForSplitBilling,
            SplitBillingReadyAt = quotation.SplitBillingReadyAt,
            AppliedAt = quotation.AppliedAt,
            Details = (quotation.QuotationDetails ?? new List<QuotationDetail>())
                .OrderBy(x => x.Id)
                .Select(x =>
                {
                    var supplementaryMeta = ResolveQuotationDetailSupplementaryMeta(quotation, x, supplementaryMetaByDetailId);
                    return new QuotationDetailResponseDto
                    {
                        // Enforce a stable business contract so frontend can split initial/supplementary
                        // without inferring from versioning rules.
                        SourceQuotationId = supplementaryMeta.SourceQuotationId,
                        ServiceSource = supplementaryMeta.ServiceSource,
                        IsSupplementary = supplementaryMeta.IsSupplementary,
                        SupplementaryReasonCode = supplementaryMeta.SupplementaryReasonCode,
                        Id = x.Id,
                        SourceQuotationDetailId = x.SourceQuotationDetailId,
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
                        ApprovalStatus = x.ApprovalStatus,
                        RejectReason = x.RejectReason,
                        Status = string.IsNullOrWhiteSpace(x.Status) ? DetailStatusActive : x.Status,
                        CancelReason = x.CancelReason,
                        CanceledBy = x.CanceledBy,
                        IsBlocking = x.IsBlocking,
                        IsMandatory = x.IsMandatory,
                        EstimatedMinutes = x.EstimatedMinutes,
                        IsReturnOldPart = x.IsReturnOldPart
                    };
                })
                .ToList()
        };
    }

    private async Task NotifyCancellationAlertAsync(Quotation quotation, QuotationDetail canceledDetail, long actorStaffId, DateTime occurredAt)
    {
        if (quotation == null || canceledDetail == null)
        {
            return;
        }

        var serviceOrder = await _context.ServiceOrders.AsNoTracking()
            .Where(x => x.Id == quotation.ServiceOrderId)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.ReceptionistId,
                x.ServiceAdvisorId
            })
            .FirstOrDefaultAsync();
        if (serviceOrder == null)
        {
            return;
        }

        var recipientIds = new HashSet<long>();
        if (serviceOrder.ReceptionistId.HasValue && serviceOrder.ReceptionistId.Value > 0)
        {
            recipientIds.Add(serviceOrder.ReceptionistId.Value);
        }
        if (serviceOrder.ServiceAdvisorId.HasValue && serviceOrder.ServiceAdvisorId.Value > 0)
        {
            recipientIds.Add(serviceOrder.ServiceAdvisorId.Value);
        }
        recipientIds.Remove(actorStaffId);

        if (recipientIds.Count == 0)
        {
            return;
        }

        var payload = new
        {
            eventType = "SupplementaryReductionSaved",
            serviceOrderId = serviceOrder.Id,
            serviceOrderCode = serviceOrder.Code,
            quotationId = quotation.Id,
            quotationDetailId = canceledDetail.Id,
            itemName = canceledDetail.ItemName,
            cancelReason = canceledDetail.CancelReason,
            occurredAt
        };

        foreach (var recipientId in recipientIds)
        {
            await _chatHubContext.Clients.Group($"User_{recipientId}")
                .SendAsync("SupplementaryReductionSaved", payload);
        }
    }

        private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
        {
            try
            {
                return await _context.Database.BeginTransactionAsync();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
