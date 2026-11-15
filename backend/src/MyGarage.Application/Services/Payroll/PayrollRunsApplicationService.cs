using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Models;

namespace MyGarage.Application.Payroll;

public sealed class PayrollRunsApplicationService : IPayrollRunsApplicationService
{
    private const string InvalidPeriodFormatMessage = "Định dạng period phải là yyyy-MM.";

    private readonly IPayrollRunDataService _dataService;

    public PayrollRunsApplicationService(IPayrollRunDataService dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
    }

    public async Task<PayrollRunEndpointActionResult<PagedEnvelope<PayrollRunEndpointPayloadDto>>> GetAllAsync(
        long? scopedBranchId,
        int? page = null,
        int? pageSize = null,
        CancellationToken ct = default)
    {
        var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
        var (pagedRuns, total) = await _dataService.GetPagedAsync(scopedBranchId, normalizedPage, normalizedSize, ct);
        var items = pagedRuns.Select(MapPayrollRunPayload).ToList();
        return PayrollRunEndpointActionResult<PagedEnvelope<PayrollRunEndpointPayloadDto>>.Ok(
            PagedEnvelope<PayrollRunEndpointPayloadDto>.Create(items, total, normalizedPage, normalizedSize));
    }

    public async Task<PayrollRunEndpointActionResult<PayrollRunPreviewEndpointDto>> PreviewAsync(
        long targetBranchId,
        string? period = null,
        CancellationToken ct = default)
    {
        if (!PayrollDomainRules.TryParsePeriod(period, out var periodCode, out var startDate, out var endDate))
        {
            return PayrollRunEndpointActionResult<PayrollRunPreviewEndpointDto>.BadRequest(InvalidPeriodFormatMessage);
        }

        var preview = await _dataService.PreviewAsync(targetBranchId, periodCode, startDate, endDate, ct);
        return PayrollRunEndpointActionResult<PayrollRunPreviewEndpointDto>.Ok(MapPreviewPayload(preview));
    }

    public async Task<PayrollRunEndpointActionResult<PayrollRunEndpointPayloadDto>> CreateAsync(
        long targetBranchId,
        long? currentUserId,
        PayrollRunCreateEndpointDto? payload,
        CancellationToken ct = default)
    {
        if (!PayrollDomainRules.TryParsePeriod(payload?.Period, out var periodCode, out var startDate, out var endDate))
        {
            return PayrollRunEndpointActionResult<PayrollRunEndpointPayloadDto>.BadRequest(InvalidPeriodFormatMessage);
        }

        var actorId = currentUserId ?? payload?.CreatedBy;
        var details = payload?.Details?
            .Where(d => d != null)
            .Select(d => new PayrollRunCreateLineInput(
                d.StaffId,
                d.BaseSalary,
                d.CommissionAmount,
                d.OvertimeAmount,
                d.AllowanceAmount,
                d.Bonus,
                d.BonusAmount,
                d.Deductions,
                d.Penalty,
                d.PenaltyAmount))
            .ToList();

        var result = await _dataService.CreateAsync(
            targetBranchId,
            periodCode,
            startDate,
            endDate,
            actorId,
            details,
            ct);

        return result.Outcome switch
        {
            PayrollRunCreateStatus.BadRequest => PayrollRunEndpointActionResult<PayrollRunEndpointPayloadDto>.BadRequest(
                result.ErrorMessage ?? string.Empty),
            PayrollRunCreateStatus.Created => PayrollRunEndpointActionResult<PayrollRunEndpointPayloadDto>.Created(
                MapPayrollRunPayload(result.Run!)),
            _ => PayrollRunEndpointActionResult<PayrollRunEndpointPayloadDto>.Ok(
                MapPayrollRunPayload(result.Run!))
        };
    }

    public async Task<PayrollRunEndpointActionResult<PayrollRunEndpointPayloadDto>> ApproveAsync(
        long id,
        long? scopedBranchId,
        long? actorId,
        CancellationToken ct = default)
    {
        var result = await _dataService.ApproveAsync(id, scopedBranchId, actorId, ct);
        return result.Outcome switch
        {
            PayrollRunApproveStatus.NotFound => PayrollRunEndpointActionResult<PayrollRunEndpointPayloadDto>.NotFound(
                result.ErrorMessage ?? string.Empty),
            PayrollRunApproveStatus.Forbidden => PayrollRunEndpointActionResult<PayrollRunEndpointPayloadDto>.Forbidden(),
            PayrollRunApproveStatus.BadRequest => PayrollRunEndpointActionResult<PayrollRunEndpointPayloadDto>.BadRequest(
                result.ErrorMessage ?? string.Empty),
            _ => PayrollRunEndpointActionResult<PayrollRunEndpointPayloadDto>.Ok(
                MapPayrollRunPayload(result.Run!))
        };
    }

    internal static decimal CalculateItemDeductions(PayrollItem item) =>
        item.AttendanceDeduction
        + item.AdvanceDeduction
        + item.LoanDeduction
        + item.InsuranceEmployeeAmount
        + item.PitAmount
        + item.OtherDeduction;

    internal static PayrollRunPreviewEndpointDto MapPreviewPayload(PayrollRunPreviewResult preview)
    {
        var details = (preview.Details ?? Array.Empty<PayrollRunPreviewDetail>())
            .Select(d => new PayrollRunPreviewEndpointDetailDto(
                d.StaffId,
                d.StaffName,
                d.BaseSalary,
                d.AllowanceAmount,
                d.CommissionAmount,
                d.OvertimeAmount,
                d.Bonus,
                d.Deductions,
                d.Penalty,
                d.NetSalary))
            .ToList();

        return new PayrollRunPreviewEndpointDto(
            preview.Period,
            preview.BranchId,
            preview.Status,
            preview.TotalAmount,
            details);
    }

    internal static PayrollRunEndpointPayloadDto MapPayrollRunPayload(PayrollRun run)
    {
        var details = (run.PayrollItems ?? Array.Empty<PayrollItem>())
            .Select(i => new PayrollRunEndpointDetailDto(
                i.Id,
                i.StaffId,
                i.Staff?.FullName,
                i.BaseSalary,
                i.AllowanceAmount,
                i.CommissionAmount,
                i.OvertimeAmount,
                i.BonusAmount,
                CalculateItemDeductions(i),
                i.PenaltyAmount,
                i.NetPay,
                i.NetPay,
                i.Status))
            .ToList();

        return new PayrollRunEndpointPayloadDto(
            run.Id,
            run.PayrollPeriodId,
            run.PayrollPeriod?.PeriodCode,
            run.RunNo,
            run.BranchId,
            run.Status,
            run.CalculatedAt,
            run.ApprovedAt,
            run.PaidAt,
            run.CreatedAt,
            details.Sum(d => d.NetSalary),
            details);
    }
}
