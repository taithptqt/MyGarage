using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Payroll;
using MyGarage.Application.Payroll.DTOs;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class PayrollRunService : IPayrollRunService
{
    private readonly IPayrollStore _store;
    private readonly IPayrollAuthorizationService _authService;
    private readonly IPayrollBatchCalculationService _batchCalculationService;

    public PayrollRunService(
        IPayrollStore store,
        IPayrollAuthorizationService authService,
        IPayrollBatchCalculationService batchCalculationService)
    {
        _store = store;
        _authService = authService;
        _batchCalculationService = batchCalculationService;
    }

    public async Task<IReadOnlyList<PayrollRunDto>> GetPayrollRunsPageAsync(
        PayrollRunPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanViewPayrollRunsAsync(query.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem danh sách bảng lương.");
        }

        var effectiveBranchId = query.BranchId.HasValue && query.BranchId.Value > 0
            ? query.BranchId
            : (!actor.IsAdmin ? actor.BranchId : null);

        var normPeriod = !string.IsNullOrWhiteSpace(query.Period) ? query.Period.Trim().ToUpperInvariant() : null;
        var normStatus = !string.IsNullOrWhiteSpace(query.Status) ? query.Status.Trim().ToUpperInvariant() : null;
        var page = Math.Max(1, query.Page.GetValueOrDefault(1));
        var pageSize = Math.Max(1, query.PageSize.GetValueOrDefault(50));

        var runs = await _store.GetPayrollRunsPageAsync(effectiveBranchId, normPeriod, normStatus, page, pageSize, ct);
        return runs.Select(MapPayrollRunToDto).ToList();
    }

    public async Task<PayrollRunDto?> GetPayrollRunByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var run = await _store.GetPayrollRunByIdAsync(id, track: false, ct);
        if (run == null) return null;

        if (!await _authService.CanViewPayrollRunsAsync(run.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem chi tiết bảng lương này.");
        }

        return MapPayrollRunToDto(run);
    }

    public async Task<PayrollRunPreviewResultDto> PreviewPayrollRunAsync(
        PayrollRunPreviewQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var targetBranchId = query.BranchId ?? actor.BranchId ?? 0;
        if (targetBranchId <= 0)
        {
            throw new ArgumentException("Mã chi nhánh (branchId) là bắt buộc.");
        }

        if (!await _authService.CanCalculatePayrollAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem trước bảng lương cho chi nhánh này.");
        }

        if (!PayrollDomainRules.TryParsePeriod(query.Period, out var periodCode, out var startDate, out var endDate))
        {
            throw new ArgumentException("Định dạng kỳ lương không hợp lệ (yyyy-MM).");
        }

        var staffs = await _store.GetActiveBranchStaffWithTierAsync(targetBranchId, ct);
        var staffIds = staffs.Select(s => s.Id).ToList();

        var profiles = await _store.GetOverlappingSalaryProfilesAsync(staffIds, startDate, endDate, ct);
        var contracts = await _store.GetActiveContractsForStaffAsync(staffIds, ct);

        var details = staffs.Select(s =>
        {
            var activeProfile = profiles.FirstOrDefault(p =>
                p.StaffId == s.Id &&
                (string.IsNullOrWhiteSpace(p.Status) || string.Equals(p.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)));
            var activeContract = contracts.FirstOrDefault(c =>
                c.StaffId == s.Id &&
                (string.IsNullOrWhiteSpace(c.Status) || string.Equals(c.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)));

            var baseSalary = activeProfile?.BaseSalary > 0m
                ? activeProfile.BaseSalary
                : (activeContract?.BaseSalary > 0m ? activeContract.BaseSalary : (s.StaffTier?.BaseSalary ?? 0m));
            var allowanceAmount = activeContract?.AllowanceAmount ?? 0m;
            var netSalary = SalaryDomainRules.CalculateNetSalary(baseSalary, allowanceAmount, 0m, 0m, 0m, 0m, 0m);

            return new PayrollRunPreviewItemDto(
                s.Id,
                s.FullName,
                baseSalary,
                allowanceAmount,
                0m,
                0m,
                0m,
                0m,
                0m,
                netSalary
            );
        }).ToList();

        return new PayrollRunPreviewResultDto(
            periodCode,
            targetBranchId,
            PayrollDomainRules.RunStatusDraft,
            details.Sum(x => x.NetSalary),
            details
        );
    }

    public async Task<PayrollRunDto> CreatePayrollRunAsync(
        PayrollRunCreateCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var targetBranchId = command.BranchId ?? actor.BranchId ?? 0;
        if (targetBranchId <= 0)
        {
            throw new ArgumentException("Mã chi nhánh (branchId) là bắt buộc.");
        }

        if (!await _authService.CanCalculatePayrollAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo bảng lương cho chi nhánh này.");
        }

        if (!PayrollDomainRules.TryParsePeriod(command.Period, out var periodCode, out var startDate, out var endDate))
        {
            throw new ArgumentException("Định dạng kỳ lương không hợp lệ (yyyy-MM).");
        }

        var now = VietnamTime.Now;
        var actorId = actor.UserId ?? command.CreatedBy;

        return await _store.ExecuteInTransactionAsync(async token =>
        {
            var periodEntity = await _store.GetPayrollPeriodByCodeAsync(periodCode, token);
            if (periodEntity != null && !PayrollDomainRules.CanAddRunToPeriod(periodEntity.Status))
            {
                throw new InvalidOperationException("Kỳ lương không ở trạng thái mở hoặc đang tính toán.");
            }

            if (periodEntity == null)
            {
                periodEntity = new PayrollPeriod
                {
                    PeriodCode = periodCode,
                    StartDate = startDate,
                    EndDate = endDate,
                    PayrollDate = endDate,
                    Status = PayrollDomainRules.PeriodStatusOpen,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _store.AddPayrollPeriodAsync(periodEntity, token);
            }

            if (command.Details == null || command.Details.Count == 0)
            {
                var batchResult = await _batchCalculationService.CalculatePayrollForAllAsync(
                    new PayrollBatchCalculationCommand
                    {
                        UserId = actorId ?? 0,
                        BranchId = targetBranchId,
                        PayrollPeriodId = periodEntity.Id,
                        OverwriteExisting = true,
                        IncludeInactiveStaff = false,
                        IssuePayslips = false
                    },
                    token);

                var calculatedRun = await _store.GetPayrollRunByIdAsync(batchResult.PayrollRunId, track: false, token)
                    ?? throw new InvalidOperationException($"Không tìm thấy bảng lương #{batchResult.PayrollRunId}.");
                return MapPayrollRunToDto(calculatedRun);
            }

            var maxRunNo = await _store.GetMaxRunNoAsync(periodEntity.Id, targetBranchId, token);
            var nextRunNo = PayrollDomainRules.CalculateNextRunNo(maxRunNo);

            var requestedStaffIds = command.Details.Select(d => d.StaffId).Where(id => id > 0).Distinct().ToList();
            var validStaffSet = await _store.GetValidBranchStaffIdsAsync(targetBranchId, requestedStaffIds, token);

            if (validStaffSet.Count == 0)
            {
                throw new InvalidOperationException("Không có nhân viên hợp lệ thuộc chi nhánh này trong bảng lương.");
            }

            var run = new PayrollRun
            {
                PayrollPeriodId = periodEntity.Id,
                BranchId = targetBranchId,
                RunNo = nextRunNo,
                Status = PayrollDomainRules.RunStatusCalculated,
                CalculatedBy = actorId,
                CalculatedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            foreach (var line in command.Details.Where(d => validStaffSet.Contains(d.StaffId)))
            {
                var baseSalary = Math.Max(0m, line.BaseSalary ?? 0m);
                var commission = Math.Max(0m, line.CommissionAmount ?? 0m);
                var overtime = Math.Max(0m, line.OvertimeAmount ?? 0m);
                var allowance = Math.Max(0m, line.AllowanceAmount ?? 0m);
                var bonus = Math.Max(0m, line.BonusAmount ?? line.Bonus ?? 0m);
                var penalty = Math.Max(0m, line.PenaltyAmount ?? line.Penalty ?? 0m);
                var deductions = Math.Max(0m, line.Deductions ?? 0m);

                var netPay = SalaryDomainRules.CalculateNetSalary(
                    baseSalary,
                    allowance,
                    commission,
                    overtime,
                    bonus,
                    deductions,
                    penalty);

                run.PayrollItems.Add(new PayrollItem
                {
                    StaffId = line.StaffId,
                    BaseSalary = baseSalary,
                    AllowanceAmount = allowance,
                    CommissionAmount = commission,
                    OvertimeAmount = overtime,
                    BonusAmount = bonus,
                    OtherDeduction = deductions,
                    PenaltyAmount = penalty,
                    NetPay = netPay,
                    Status = PayrollDomainRules.ItemStatusDraft,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await _store.AddPayrollRunAsync(run, token);
            var createdRun = await _store.GetPayrollRunByIdAsync(run.Id, track: false, token)
                ?? throw new InvalidOperationException($"Không tìm thấy bảng lương #{run.Id}.");
            return MapPayrollRunToDto(createdRun);
        }, ct);
    }

    public async Task<PayrollRunDto> ApprovePayrollRunAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        if (!await _authService.CanApprovePayrollAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền duyệt bảng lương này.");
        }

        return await _store.ExecuteInTransactionAsync(async token =>
        {
            var run = await _store.GetPayrollRunByIdAsync(id, track: true, token);
            if (run == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy bảng lương #{id}.");
            }

            if (!PayrollDomainRules.CanApprovePayrollRun(run.Status, run.PayrollItems.Count, out var error))
            {
                throw new InvalidOperationException(error ?? "Bảng lương không đủ điều kiện duyệt.");
            }

            var now = VietnamTime.Now;
            run.Status = PayrollDomainRules.RunStatusApproved;
            run.ApprovedBy = actor.UserId;
            run.ApprovedAt = now;
            run.UpdatedAt = now;

            foreach (var item in run.PayrollItems)
            {
                if (string.Equals(item.Status, PayrollDomainRules.ItemStatusDraft, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.Status, PayrollDomainRules.RunStatusCalculated, StringComparison.OrdinalIgnoreCase))
                {
                    item.Status = PayrollDomainRules.ItemStatusApproved;
                    item.UpdatedAt = now;
                }
            }

            await _store.SaveChangesAsync(token);
            return MapPayrollRunToDto(run);
        }, ct);
    }

    public async Task<IReadOnlyList<PayslipDto>> GetMyPayslipsAsync(
        PayslipsQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var targetStaffId = query.StaffId ?? actor.UserId ?? 0;
        if (targetStaffId <= 0)
        {
            throw new ArgumentException("Không xác định được nhân viên.");
        }

        if (!await _authService.CanViewMyPayslipAsync(targetStaffId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem phiếu lương của nhân viên này.");
        }

        var normPeriod = !string.IsNullOrWhiteSpace(query.Period) ? query.Period.Trim().ToUpperInvariant() : null;
        return await _store.GetMyPayslipsAsync(targetStaffId, normPeriod, ct);
    }

    private static PayrollRunDto MapPayrollRunToDto(PayrollRun run)
    {
        var details = run.PayrollItems.Select(i => new PayrollRunItemDto(
            i.Id,
            i.StaffId,
            i.Staff?.FullName,
            i.BaseSalary,
            i.AllowanceAmount,
            i.CommissionAmount,
            i.OvertimeAmount,
            i.BonusAmount,
            i.AttendanceDeduction,
            i.AdvanceDeduction,
            i.LoanDeduction,
            i.InsuranceEmployeeAmount,
            i.PitAmount,
            i.OtherDeduction,
            i.PenaltyAmount,
            i.NetPay,
            i.Status
        )).ToList();

        var totalAmount = details.Sum(d => d.NetPay);

        return new PayrollRunDto(
            run.Id,
            run.PayrollPeriodId,
            run.PayrollPeriod?.PeriodCode,
            run.BranchId,
            null,
            run.RunNo,
            run.Status,
            totalAmount,
            details.Count,
            run.CalculatedBy,
            run.CalculatedAt,
            run.ApprovedBy,
            run.ApprovedAt,
            run.PaidAt,
            details
        );
    }
}
