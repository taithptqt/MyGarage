using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Payroll;
using MyGarage.Application.Payroll.DTOs;
using MyGarage.Domain.Rules;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class PayrollApplicationService : IPayrollApplicationService
{
    private readonly IPayrollStore _store;
    private readonly IPayrollAuthorizationService _authService;
    private readonly IPayrollRunService _payrollRunService;

    public PayrollApplicationService(
        IPayrollStore store,
        IPayrollAuthorizationService authService,
        IPayrollRunService payrollRunService)
    {
        _store = store;
        _authService = authService;
        _payrollRunService = payrollRunService;
    }

    internal PayrollApplicationService(
        IPayrollStore store,
        IPayrollAuthorizationService authService,
        IPayrollBatchCalculationService batchCalculationService)
        : this(store, authService, new PayrollRunService(store, authService, batchCalculationService))
    {
    }

    public async Task<SalaryProfilePageResultDto> GetSalaryProfilesPageAsync(
        SalaryProfilePageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanViewSalaryProfilesAsync(query.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem hồ sơ lương.");
        }

        var effectiveBranchId = query.BranchId.HasValue && query.BranchId.Value > 0
            ? query.BranchId
            : (!actor.IsAdmin ? actor.BranchId : null);

        var normStatus = !string.IsNullOrWhiteSpace(query.Status)
            ? query.Status.Trim().ToUpperInvariant()
            : null;

        var page = Math.Max(1, query.Page.GetValueOrDefault(1));
        var pageSize = Math.Max(1, query.PageSize.GetValueOrDefault(50));

        var (rows, totalCount) = await _store.GetSalaryProfilesPageAsync(
            effectiveBranchId,
            query.StaffId,
            normStatus,
            page,
            pageSize,
            ct);

        return new SalaryProfilePageResultDto(rows, totalCount, page, pageSize);
    }

    public async Task<SalaryProfileDto?> GetSalaryProfileByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var profile = await _store.GetSalaryProfileWithRelationsAsync(id, track: false, ct);
        if (profile == null) return null;

        if (!await _authService.CanViewSalaryProfilesAsync(profile.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem hồ sơ lương này.");
        }

        return MapSalaryProfileToDto(profile, profile.Staff.FullName, profile.Branch?.Name);
    }

    public async Task<SalaryProfileDto> CreateSalaryProfileAsync(
        SalaryProfileUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!SalaryDomainRules.ValidateSalaryProfile(command.BaseSalary, command.EffectiveFrom, command.EffectiveTo, out var error))
        {
            throw new ArgumentException(error ?? "Dữ liệu hồ sơ lương không hợp lệ.");
        }

        var staff = await _store.GetActiveStaffAsync(command.StaffId, ct);
        if (staff == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy nhân viên #{command.StaffId}.");
        }

        var targetBranchId = command.BranchId ?? staff.BranchId;
        if (!await _authService.CanManageSalaryProfilesAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo hồ sơ lương.");
        }

        var fromDate = DateOnly.FromDateTime(command.EffectiveFrom!.Value);
        var toDate = command.EffectiveTo.HasValue ? DateOnly.FromDateTime(command.EffectiveTo.Value) : (DateOnly?)null;

        return await _store.ExecuteInTransactionAsync(async token =>
        {
            var existingProfiles = await _store.GetActiveSalaryProfilesForStaffAsync(command.StaffId, null, token);
            foreach (var ep in existingProfiles)
            {
                if (SalaryDomainRules.CheckProfileDateOverlap(fromDate, toDate, ep.EffectiveFrom, ep.EffectiveTo))
                {
                    throw new InvalidOperationException("Nhân viên đã có hồ sơ lương hiệu lực trong khoảng thời gian này.");
                }
            }

            var now = VietnamTime.Now;
            var profile = new StaffSalaryProfile
            {
                StaffId = command.StaffId,
                BranchId = targetBranchId,
                BaseSalary = command.BaseSalary!.Value,
                EffectiveFrom = fromDate,
                EffectiveTo = toDate,
                Status = string.IsNullOrWhiteSpace(command.Status) ? nameof(StaffSalaryProfileStatusCode.ACTIVE) : command.Status.Trim().ToUpperInvariant(),
                Note = command.Note,
                UpdatedBy = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _store.AddSalaryProfileAsync(profile, token);
            return MapSalaryProfileToDto(profile, staff.FullName, null);
        }, ct);
    }

    public async Task<SalaryProfileDto> UpdateSalaryProfileAsync(
        long id,
        SalaryProfileUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        if (!SalaryDomainRules.ValidateSalaryProfile(command.BaseSalary, command.EffectiveFrom, command.EffectiveTo, out var error))
        {
            throw new ArgumentException(error ?? "Dữ liệu hồ sơ lương không hợp lệ.");
        }

        var profile = await _store.GetSalaryProfileWithRelationsAsync(id, track: true, ct);
        if (profile == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ lương #{id}.");
        }

        var targetBranchId = command.BranchId ?? profile.BranchId;
        if (!await _authService.CanManageSalaryProfilesAsync(targetBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền chỉnh sửa hồ sơ lương.");
        }

        var fromDate = DateOnly.FromDateTime(command.EffectiveFrom!.Value);
        var toDate = command.EffectiveTo.HasValue ? DateOnly.FromDateTime(command.EffectiveTo.Value) : (DateOnly?)null;

        return await _store.ExecuteInTransactionAsync(async token =>
        {
            var otherProfiles = await _store.GetActiveSalaryProfilesForStaffAsync(profile.StaffId, id, token);
            foreach (var ep in otherProfiles)
            {
                if (SalaryDomainRules.CheckProfileDateOverlap(fromDate, toDate, ep.EffectiveFrom, ep.EffectiveTo))
                {
                    throw new InvalidOperationException("Khoảng thời gian hiệu lực bị trùng lặp với hồ sơ lương khác của nhân viên.");
                }
            }

            profile.BranchId = targetBranchId;
            profile.BaseSalary = command.BaseSalary!.Value;
            profile.EffectiveFrom = fromDate;
            profile.EffectiveTo = toDate;
            if (!string.IsNullOrWhiteSpace(command.Status))
            {
                profile.Status = command.Status.Trim().ToUpperInvariant();
            }
            profile.Note = command.Note;
            profile.UpdatedBy = actor.UserId;
            profile.UpdatedAt = VietnamTime.Now;

            await _store.SaveChangesAsync(token);
            return MapSalaryProfileToDto(profile, profile.Staff.FullName, null);
        }, ct);
    }

    public async Task<bool> DeleteSalaryProfileAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return false;

        var profile = await _store.GetSalaryProfileWithRelationsAsync(id, track: true, ct);
        if (profile == null) return false;

        if (!await _authService.CanManageSalaryProfilesAsync(profile.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xóa hồ sơ lương.");
        }

        await _store.RemoveSalaryProfileAsync(profile, ct);
        return true;
    }

    public async Task<IReadOnlyList<SalaryRequestDto>> GetSalaryRequestsPageAsync(
        SalaryRequestPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanViewSalaryProfilesAsync(query.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem yêu cầu phê duyệt lương.");
        }

        return await _store.GetSalaryRequestsAsync(ct);
    }

    public async Task<bool> ApproveSalaryRequestAsync(
        long requestId,
        SalaryApprovalDecisionCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));

        if (!await _authService.CanManageSalaryProfilesAsync(null, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền duyệt yêu cầu lương.");
        }

        if (!await _store.HasSalaryRequestCrudSupportAsync())
        {
            return false;
        }

        var status = await _store.GetSalaryRequestStatusAsync(requestId, ct);
        if (status == null) return false;

        if (!SalaryDomainRules.ValidateApprovalDecision(status, command.BaseSalary, command.EffectiveFrom, out var error))
        {
            throw new InvalidOperationException(error ?? "Yêu cầu không đủ điều kiện duyệt.");
        }

        var now = VietnamTime.Now;
        StaffSalaryProfile? createdProfile = null;
        if (command.StaffId.HasValue && command.BaseSalary.HasValue && command.BaseSalary.Value > 0m)
        {
            var effectiveDate = command.EffectiveFrom.HasValue
                ? DateOnly.FromDateTime(command.EffectiveFrom.Value)
                : DateOnly.FromDateTime(now);

            createdProfile = new StaffSalaryProfile
            {
                StaffId = command.StaffId.Value,
                BaseSalary = command.BaseSalary.Value,
                EffectiveFrom = effectiveDate,
                Status = nameof(StaffSalaryProfileStatusCode.ACTIVE),
                Note = command.Note ?? "Phê duyệt từ yêu cầu khởi tạo lương",
                UpdatedBy = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        await _store.ApproveSalaryRequestAsync(requestId, actor.UserId, now, createdProfile, ct);
        return true;
    }

    public async Task<bool> RejectSalaryRequestAsync(
        long requestId,
        SalaryApprovalDecisionCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));

        if (!await _authService.CanManageSalaryProfilesAsync(null, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền từ chối yêu cầu lương.");
        }

        if (!await _store.HasSalaryRequestCrudSupportAsync())
        {
            return false;
        }

        var status = await _store.GetSalaryRequestStatusAsync(requestId, ct);
        if (status == null) return false;

        if (!string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chỉ có thể từ chối yêu cầu ở trạng thái PENDING.");
        }

        await _store.RejectSalaryRequestAsync(
            requestId,
            actor.UserId,
            command.RejectReason ?? "Từ chối bởi cấp quản lý",
            VietnamTime.Now,
            ct);
        return true;
    }

    public Task<IReadOnlyList<PayrollRunDto>> GetPayrollRunsPageAsync(PayrollRunPageQuery query, UserActorContext actor, CancellationToken ct = default)
        => _payrollRunService.GetPayrollRunsPageAsync(query, actor, ct);

    public Task<PayrollRunDto?> GetPayrollRunByIdAsync(long id, UserActorContext actor, CancellationToken ct = default)
        => _payrollRunService.GetPayrollRunByIdAsync(id, actor, ct);

    public Task<PayrollRunPreviewResultDto> PreviewPayrollRunAsync(PayrollRunPreviewQuery query, UserActorContext actor, CancellationToken ct = default)
        => _payrollRunService.PreviewPayrollRunAsync(query, actor, ct);

    public Task<PayrollRunDto> CreatePayrollRunAsync(PayrollRunCreateCommand command, UserActorContext actor, CancellationToken ct = default)
        => _payrollRunService.CreatePayrollRunAsync(command, actor, ct);

    public Task<PayrollRunDto> ApprovePayrollRunAsync(long id, UserActorContext actor, CancellationToken ct = default)
        => _payrollRunService.ApprovePayrollRunAsync(id, actor, ct);

    public Task<IReadOnlyList<PayslipDto>> GetMyPayslipsAsync(PayslipsQuery query, UserActorContext actor, CancellationToken ct = default)
        => _payrollRunService.GetMyPayslipsAsync(query, actor, ct);

    private static SalaryProfileDto MapSalaryProfileToDto(StaffSalaryProfile profile, string staffName, string? branchName)
        => new(
            profile.Id,
            profile.StaffId,
            staffName,
            profile.BranchId,
            branchName,
            profile.BaseSalary,
            profile.EffectiveFrom,
            profile.EffectiveTo,
            profile.Status,
            profile.Note,
            profile.UpdatedBy,
            profile.CreatedAt,
            profile.UpdatedAt
        );
}
