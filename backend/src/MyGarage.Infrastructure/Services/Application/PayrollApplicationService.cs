using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    private readonly IGarageContext _context;
    private readonly IPayrollAuthorizationService _authService;
    private readonly IPayrollBatchCalculationService _batchCalculationService;
    private readonly IAccountingCrudService? _crud;

    public PayrollApplicationService(
        IGarageContext context,
        IPayrollAuthorizationService authService,
        IPayrollBatchCalculationService batchCalculationService,
        IAccountingCrudService? crud = null)
    {
        _context = context;
        _authService = authService;
        _batchCalculationService = batchCalculationService;
        _crud = crud;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task<SalaryProfilePageResultDto> GetSalaryProfilesPageAsync(
        SalaryProfilePageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var canView = await _authService.CanViewSalaryProfilesAsync(query.BranchId, actor, ct);
        if (!canView)
        {
            throw new UnauthorizedAccessException("Không có quyền xem hồ sơ lương.");
        }

        var dbQuery = _context.StaffSalaryProfiles
            .AsNoTracking()
            .Include(p => p.Staff)
            .Include(p => p.Branch)
            .AsQueryable();

        if (query.BranchId.HasValue && query.BranchId.Value > 0)
        {
            dbQuery = dbQuery.Where(p => p.BranchId == query.BranchId.Value);
        }
        else if (actor.BranchId.HasValue && !actor.IsAdmin)
        {
            dbQuery = dbQuery.Where(p => p.BranchId == actor.BranchId.Value);
        }

        if (query.StaffId.HasValue && query.StaffId.Value > 0)
        {
            dbQuery = dbQuery.Where(p => p.StaffId == query.StaffId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var normStatus = query.Status.Trim().ToUpperInvariant();
            dbQuery = dbQuery.Where(p => p.Status.ToUpper() == normStatus);
        }

        var totalCount = await dbQuery.CountAsync(ct);

        var page = query.Page.GetValueOrDefault(1);
        if (page < 1) page = 1;
        var pageSize = query.PageSize.GetValueOrDefault(50);
        if (pageSize < 1) pageSize = 50;

        var rows = await dbQuery
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new SalaryProfileDto(
                p.Id,
                p.StaffId,
                p.Staff.FullName,
                p.BranchId,
                p.Branch != null ? p.Branch.Name : null,
                p.BaseSalary,
                p.EffectiveFrom,
                p.EffectiveTo,
                p.Status,
                p.Note,
                p.UpdatedBy,
                p.CreatedAt,
                p.UpdatedAt
            ))
            .ToListAsync(ct);

        return new SalaryProfilePageResultDto(rows, totalCount, page, pageSize);
    }

    public async Task<SalaryProfileDto?> GetSalaryProfileByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var profile = await _context.StaffSalaryProfiles
            .AsNoTracking()
            .Include(p => p.Staff)
            .Include(p => p.Branch)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (profile == null) return null;

        var canView = await _authService.CanViewSalaryProfilesAsync(profile.BranchId, actor, ct);
        if (!canView)
        {
            throw new UnauthorizedAccessException("Không có quyền xem hồ sơ lương này.");
        }

        return new SalaryProfileDto(
            profile.Id,
            profile.StaffId,
            profile.Staff.FullName,
            profile.BranchId,
            profile.Branch != null ? profile.Branch.Name : null,
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

    public async Task<SalaryProfileDto> CreateSalaryProfileAsync(
        SalaryProfileUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!SalaryDomainRules.ValidateSalaryProfile(command.BaseSalary, command.EffectiveFrom, command.EffectiveTo, out var error))
        {
            throw new ArgumentException(error ?? "Dữ liệu hồ sơ lương không hợp lệ.");
        }

        var staff = await _context.Staffs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == command.StaffId && s.IsDeleted != true, ct);
        if (staff == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy nhân viên #{command.StaffId}.");
        }

        var targetBranchId = command.BranchId ?? staff.BranchId;
        var canManage = await _authService.CanManageSalaryProfilesAsync(targetBranchId, actor, ct);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("Không có quyền tạo hồ sơ lương.");
        }

        var fromDate = DateOnly.FromDateTime(command.EffectiveFrom!.Value);
        var toDate = command.EffectiveTo.HasValue ? DateOnly.FromDateTime(command.EffectiveTo.Value) : (DateOnly?)null;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var existingProfiles = await _context.StaffSalaryProfiles
                .Where(p => p.StaffId == command.StaffId && p.Status == nameof(StaffSalaryProfileStatusCode.ACTIVE))
                .ToListAsync(ct);

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

            _context.StaffSalaryProfiles.Add(profile);
            await _context.SaveChangesAsync(ct);

            return new SalaryProfileDto(
                profile.Id,
                profile.StaffId,
                staff.FullName,
                profile.BranchId,
                null,
                profile.BaseSalary,
                profile.EffectiveFrom,
                profile.EffectiveTo,
                profile.Status,
                profile.Note,
                profile.UpdatedBy,
                profile.CreatedAt,
                profile.UpdatedAt
            );
        });
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

        var profile = await _context.StaffSalaryProfiles
            .Include(p => p.Staff)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (profile == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ lương #{id}.");
        }

        var targetBranchId = command.BranchId ?? profile.BranchId;
        var canManage = await _authService.CanManageSalaryProfilesAsync(targetBranchId, actor, ct);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("Không có quyền chỉnh sửa hồ sơ lương.");
        }

        var fromDate = DateOnly.FromDateTime(command.EffectiveFrom!.Value);
        var toDate = command.EffectiveTo.HasValue ? DateOnly.FromDateTime(command.EffectiveTo.Value) : (DateOnly?)null;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var otherProfiles = await _context.StaffSalaryProfiles
                .Where(p => p.StaffId == profile.StaffId && p.Id != id && p.Status == nameof(StaffSalaryProfileStatusCode.ACTIVE))
                .ToListAsync(ct);

            foreach (var ep in otherProfiles)
            {
                if (SalaryDomainRules.CheckProfileDateOverlap(fromDate, toDate, ep.EffectiveFrom, ep.EffectiveTo))
                {
                    throw new InvalidOperationException("Khoảng thời gian hiệu lực bị trùng lặp với hồ sơ lương khác của nhân viên.");
                }
            }

            var now = VietnamTime.Now;
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
            profile.UpdatedAt = now;

            await _context.SaveChangesAsync(ct);

            return new SalaryProfileDto(
                profile.Id,
                profile.StaffId,
                profile.Staff.FullName,
                profile.BranchId,
                null,
                profile.BaseSalary,
                profile.EffectiveFrom,
                profile.EffectiveTo,
                profile.Status,
                profile.Note,
                profile.UpdatedBy,
                profile.CreatedAt,
                profile.UpdatedAt
            );
        });
    }

    public async Task<bool> DeleteSalaryProfileAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return false;

        var profile = await _context.StaffSalaryProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (profile == null) return false;

        var canManage = await _authService.CanManageSalaryProfilesAsync(profile.BranchId, actor, ct);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("Không có quyền xóa hồ sơ lương.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.StaffSalaryProfiles.Remove(profile);
            await _context.SaveChangesAsync(ct);
            return true;
        });
    }

    public async Task<IReadOnlyList<SalaryRequestDto>> GetSalaryRequestsPageAsync(
        SalaryRequestPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var canView = await _authService.CanViewSalaryProfilesAsync(query.BranchId, actor, ct);
        if (!canView)
        {
            throw new UnauthorizedAccessException("Không có quyền xem yêu cầu phê duyệt lương.");
        }

        if (_crud != null)
        {
            var sql = """
                SELECT
                    r.id,
                    r.request_type,
                    r.status,
                    r.amount,
                    r.reason,
                    r.reject_reason,
                    r.created_at,
                    r.requested_by,
                    req.FullName AS requested_by_name,
                    TRY_CONVERT(bigint, JSON_VALUE(r.request_payload, '$.staffId')) AS staff_id,
                    JSON_VALUE(r.request_payload, '$.staffName') AS staff_name,
                    TRY_CONVERT(bigint, JSON_VALUE(r.request_payload, '$.branchId')) AS branch_id,
                    TRY_CONVERT(decimal(18,2), JSON_VALUE(r.request_payload, '$.baseSalary')) AS proposed_base_salary,
                    TRY_CONVERT(date, JSON_VALUE(r.request_payload, '$.effectiveFrom')) AS effective_from,
                    JSON_VALUE(r.request_payload, '$.note') AS note
                FROM dbo.finance_approval_requests r
                LEFT JOIN dbo.Staffs req ON req.Id = r.requested_by
                WHERE r.request_type = 'PAYROLL_ADJUSTMENT'
                  AND JSON_VALUE(r.request_payload, '$.requestSubtype') = 'STAFF_INITIAL_SALARY'
                ORDER BY r.created_at DESC, r.id DESC;
                """;
            var rows = await _crud.QueryAsync(sql, []);
            return rows.Select(row => new SalaryRequestDto(
                Convert.ToInt64(row.GetValueOrDefault("id")),
                Convert.ToInt64(row.GetValueOrDefault("staff_id") ?? row.GetValueOrDefault("requested_by") ?? 0),
                Convert.ToString(row.GetValueOrDefault("staff_name") ?? row.GetValueOrDefault("requested_by_name")),
                row.TryGetValue("branch_id", out var bId) && bId != null && bId != DBNull.Value ? Convert.ToInt64(bId) : null,
                row.TryGetValue("proposed_base_salary", out var s) && s != null && s != DBNull.Value ? Convert.ToDecimal(s) : null,
                row.TryGetValue("effective_from", out var ef) && ef != null && ef != DBNull.Value ? Convert.ToDateTime(ef) : null,
                Convert.ToString(row.GetValueOrDefault("status")) ?? "",
                Convert.ToString(row.GetValueOrDefault("reason")),
                Convert.ToString(row.GetValueOrDefault("reject_reason")),
                Convert.ToDateTime(row.GetValueOrDefault("created_at"))
            )).ToList();
        }

        return Array.Empty<SalaryRequestDto>();
    }

    public async Task<bool> ApproveSalaryRequestAsync(
        long requestId,
        SalaryApprovalDecisionCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));

        var canManage = await _authService.CanManageSalaryProfilesAsync(null, actor, ct);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("Không có quyền duyệt yêu cầu lương.");
        }

        if (_crud != null)
        {
            var row = (await _crud.QueryAsync(
                "SELECT id, status FROM dbo.finance_approval_requests WHERE id = @id",
                [new SqlParameter("@id", requestId)])).FirstOrDefault();
            if (row == null) return false;

            var status = Convert.ToString(row.GetValueOrDefault("status")) ?? "";
            if (!SalaryDomainRules.ValidateApprovalDecision(status, command.BaseSalary, command.EffectiveFrom, out var error))
            {
                throw new InvalidOperationException(error ?? "Yêu cầu không đủ điều kiện duyệt.");
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                var now = VietnamTime.Now;
                await _crud.ExecuteAsync(
                    "UPDATE dbo.finance_approval_requests SET status = 'APPROVED', approver_id = @approver, updated_at = @now WHERE id = @id",
                    [
                        new SqlParameter("@approver", (object?)actor.UserId ?? DBNull.Value),
                        new SqlParameter("@now", now),
                        new SqlParameter("@id", requestId)
                    ]);

                if (command.StaffId.HasValue && command.BaseSalary.HasValue && command.BaseSalary.Value > 0m)
                {
                    var effectiveDate = command.EffectiveFrom.HasValue
                        ? DateOnly.FromDateTime(command.EffectiveFrom.Value)
                        : DateOnly.FromDateTime(now);

                    var profile = new StaffSalaryProfile
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
                    _context.StaffSalaryProfiles.Add(profile);
                    await _context.SaveChangesAsync(ct);
                }

                return true;
            });
        }

        return false;
    }

    public async Task<bool> RejectSalaryRequestAsync(
        long requestId,
        SalaryApprovalDecisionCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));

        var canManage = await _authService.CanManageSalaryProfilesAsync(null, actor, ct);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("Không có quyền từ chối yêu cầu lương.");
        }

        if (_crud != null)
        {
            var row = (await _crud.QueryAsync(
                "SELECT id, status FROM dbo.finance_approval_requests WHERE id = @id",
                [new SqlParameter("@id", requestId)])).FirstOrDefault();
            if (row == null) return false;

            var status = Convert.ToString(row.GetValueOrDefault("status")) ?? "";
            if (!string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Chỉ có thể từ chối yêu cầu ở trạng thái PENDING.");
            }

            var now = VietnamTime.Now;
            await _crud.ExecuteAsync(
                "UPDATE dbo.finance_approval_requests SET status = 'REJECTED', approver_id = @approver, reject_reason = @reason, updated_at = @now WHERE id = @id",
                [
                    new SqlParameter("@approver", (object?)actor.UserId ?? DBNull.Value),
                    new SqlParameter("@reason", (object?)command.RejectReason ?? "Từ chối bởi cấp quản lý"),
                    new SqlParameter("@now", now),
                    new SqlParameter("@id", requestId)
                ]);

            return true;
        }

        return false;
    }

    public async Task<IReadOnlyList<PayrollRunDto>> GetPayrollRunsPageAsync(
        PayrollRunPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var canView = await _authService.CanViewPayrollRunsAsync(query.BranchId, actor, ct);
        if (!canView)
        {
            throw new UnauthorizedAccessException("Không có quyền xem danh sách bảng lương.");
        }

        var dbQuery = _context.PayrollRuns
            .AsNoTracking()
            .Include(r => r.PayrollPeriod)
            .Include(r => r.PayrollItems)
                .ThenInclude(i => i.Staff)
            .Where(r => r.Status != nameof(PayrollRunStatusCode.CANCELED))
            .AsQueryable();

        if (query.BranchId.HasValue && query.BranchId.Value > 0)
        {
            dbQuery = dbQuery.Where(r => r.BranchId == query.BranchId.Value);
        }
        else if (actor.BranchId.HasValue && !actor.IsAdmin)
        {
            dbQuery = dbQuery.Where(r => r.BranchId == actor.BranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Period))
        {
            var normPeriod = query.Period.Trim().ToUpperInvariant();
            dbQuery = dbQuery.Where(r => r.PayrollPeriod.PeriodCode.ToUpper() == normPeriod);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var normStatus = query.Status.Trim().ToUpperInvariant();
            dbQuery = dbQuery.Where(r => r.Status.ToUpper() == normStatus);
        }

        var page = query.Page.GetValueOrDefault(1);
        if (page < 1) page = 1;
        var pageSize = query.PageSize.GetValueOrDefault(50);
        if (pageSize < 1) pageSize = 50;

        var runs = await dbQuery
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return runs.Select(MapPayrollRunToDto).ToList();
    }

    public async Task<PayrollRunDto?> GetPayrollRunByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var run = await _context.PayrollRuns
            .AsNoTracking()
            .Include(r => r.PayrollPeriod)
            .Include(r => r.PayrollItems)
                .ThenInclude(i => i.Staff)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (run == null) return null;

        var canView = await _authService.CanViewPayrollRunsAsync(run.BranchId, actor, ct);
        if (!canView)
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

        var canCalc = await _authService.CanCalculatePayrollAsync(targetBranchId, actor, ct);
        if (!canCalc)
        {
            throw new UnauthorizedAccessException("Không có quyền xem trước bảng lương cho chi nhánh này.");
        }

        if (!PayrollDomainRules.TryParsePeriod(query.Period, out var periodCode, out var startDate, out var endDate))
        {
            throw new ArgumentException("Định dạng kỳ lương không hợp lệ (yyyy-MM).");
        }

        var staffs = await _context.Staffs
            .AsNoTracking()
            .Include(s => s.StaffTier)
            .Where(s => s.BranchId == targetBranchId && s.IsDeleted != true && (s.Status == null || s.Status != "INACTIVE"))
            .ToListAsync(ct);

        var staffIds = staffs.Select(s => s.Id).ToList();

        var profiles = await _context.StaffSalaryProfiles
            .AsNoTracking()
            .Where(p => staffIds.Contains(p.StaffId) && p.EffectiveFrom <= endDate && (p.EffectiveTo == null || p.EffectiveTo >= startDate))
            .ToListAsync(ct);

        var contracts = await _context.StaffContracts
            .AsNoTracking()
            .Where(c => staffIds.Contains(c.StaffId) && c.Status == nameof(StaffContractStatusCode.ACTIVE))
            .ToListAsync(ct);

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
            var commissionAmount = 0m;
            var overtimeAmount = 0m;
            var bonus = 0m;
            var deductions = 0m;
            var penalty = 0m;

            var netSalary = SalaryDomainRules.CalculateNetSalary(
                baseSalary,
                allowanceAmount,
                commissionAmount,
                overtimeAmount,
                bonus,
                deductions,
                penalty);

            return new PayrollRunPreviewItemDto(
                s.Id,
                s.FullName,
                baseSalary,
                allowanceAmount,
                commissionAmount,
                overtimeAmount,
                bonus,
                deductions,
                penalty,
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

        var canCalc = await _authService.CanCalculatePayrollAsync(targetBranchId, actor, ct);
        if (!canCalc)
        {
            throw new UnauthorizedAccessException("Không có quyền tạo bảng lương cho chi nhánh này.");
        }

        if (!PayrollDomainRules.TryParsePeriod(command.Period, out var periodCode, out var startDate, out var endDate))
        {
            throw new ArgumentException("Định dạng kỳ lương không hợp lệ (yyyy-MM).");
        }

        var now = VietnamTime.Now;
        var actorId = actor.UserId ?? command.CreatedBy;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var periodEntity = await _context.PayrollPeriods
                .FirstOrDefaultAsync(p => p.PeriodCode == periodCode, ct);

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
                _context.PayrollPeriods.Add(periodEntity);
                await _context.SaveChangesAsync(ct);
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
                    ct);

                var calculatedRun = await _context.PayrollRuns
                    .AsNoTracking()
                    .Include(r => r.PayrollPeriod)
                    .Include(r => r.PayrollItems)
                        .ThenInclude(i => i.Staff)
                    .FirstAsync(r => r.Id == batchResult.PayrollRunId, ct);

                return MapPayrollRunToDto(calculatedRun);
            }

            var maxRunNo = await _context.PayrollRuns
                .AsNoTracking()
                .Where(r => r.PayrollPeriodId == periodEntity.Id && r.BranchId == targetBranchId)
                .MaxAsync(r => (int?)r.RunNo, ct) ?? 0;
            var nextRunNo = PayrollDomainRules.CalculateNextRunNo(maxRunNo);

            var requestedStaffIds = command.Details.Select(d => d.StaffId).Where(id => id > 0).Distinct().ToList();
            var validStaffIds = await _context.Staffs
                .AsNoTracking()
                .Where(s => requestedStaffIds.Contains(s.Id) && s.BranchId == targetBranchId && s.IsDeleted != true)
                .Select(s => s.Id)
                .ToListAsync(ct);
            var validStaffSet = validStaffIds.ToHashSet();

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

            _context.PayrollRuns.Add(run);
            await _context.SaveChangesAsync(ct);

            var createdRun = await _context.PayrollRuns
                .AsNoTracking()
                .Include(r => r.PayrollPeriod)
                .Include(r => r.PayrollItems)
                    .ThenInclude(i => i.Staff)
                .FirstAsync(r => r.Id == run.Id, ct);

            return MapPayrollRunToDto(createdRun);
        });
    }

    public async Task<PayrollRunDto> ApprovePayrollRunAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var canApprove = await _authService.CanApprovePayrollAsync(id, actor, ct);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("Không có quyền duyệt bảng lương này.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var run = await _context.PayrollRuns
                .Include(r => r.PayrollPeriod)
                .Include(r => r.PayrollItems)
                    .ThenInclude(i => i.Staff)
                .FirstOrDefaultAsync(r => r.Id == id, ct);

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

            await _context.SaveChangesAsync(ct);
            return MapPayrollRunToDto(run);
        });
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

        var canView = await _authService.CanViewMyPayslipAsync(targetStaffId, actor, ct);
        if (!canView)
        {
            throw new UnauthorizedAccessException("Không có quyền xem phiếu lương của nhân viên này.");
        }

        var dbQuery = _context.Payslips
            .AsNoTracking()
            .Include(p => p.PayrollItem)
                .ThenInclude(pi => pi.PayrollRun)
                    .ThenInclude(pr => pr.PayrollPeriod)
            .Include(p => p.PayrollItem)
                .ThenInclude(pi => pi.Staff)
            .Where(p => p.PayrollItem.StaffId == targetStaffId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Period))
        {
            var normPeriod = query.Period.Trim().ToUpperInvariant();
            dbQuery = dbQuery.Where(p => p.PayrollItem.PayrollRun.PayrollPeriod.PeriodCode.ToUpper() == normPeriod);
        }

        var list = await dbQuery
            .OrderByDescending(p => p.IssuedAt)
            .Select(p => new PayslipDto(
                p.Id,
                p.PayrollItemId,
                p.SlipNo,
                p.PayrollItem.StaffId,
                p.PayrollItem.Staff.FullName,
                p.PayrollItem.PayrollRun.PayrollPeriod.PeriodCode,
                p.PayrollItem.BaseSalary,
                p.PayrollItem.AllowanceAmount,
                p.PayrollItem.CommissionAmount,
                p.PayrollItem.OvertimeAmount,
                p.PayrollItem.BonusAmount,
                p.PayrollItem.AttendanceDeduction
                    + p.PayrollItem.AdvanceDeduction
                    + p.PayrollItem.LoanDeduction
                    + p.PayrollItem.InsuranceEmployeeAmount
                    + p.PayrollItem.PitAmount
                    + p.PayrollItem.OtherDeduction
                    + p.PayrollItem.PenaltyAmount,
                p.PayrollItem.NetPay,
                p.IssuedAt,
                p.ViewedAt
            ))
            .ToListAsync(ct);

        return list;
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
