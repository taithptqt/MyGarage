using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Common;
using MyGarage.Application.Payroll;
using MyGarage.Application.Payroll.DTOs;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Payroll;

public sealed partial class EfPayrollStore : IPayrollStore
{
    private readonly IGarageContext _context;
    private readonly IAccountingSqlQueryService? _sqlQuery;
    private readonly IAccountingSqlCommandService? _sqlCommand;

    public EfPayrollStore(
        IGarageContext context,
        IAccountingSqlQueryService? sqlQuery = null,
        IAccountingSqlCommandService? sqlCommand = null)
    {
        _context = context;
        _sqlQuery = sqlQuery;
        _sqlCommand = sqlCommand;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        try
        {
            return await _context.Database.BeginTransactionAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("SqlServerRetryingExecutionStrategy"))
            {
                return null;
            }
            throw;
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(ct);
            try
            {
                var result = await action(ct);
                if (tx != null) await tx.CommitAsync(ct);
                return result;
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<(IReadOnlyList<SalaryProfileDto> Items, int TotalCount)> GetSalaryProfilesPageAsync(
        long? branchId,
        long? staffId,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var dbQuery = _context.StaffSalaryProfiles
            .AsNoTracking()
            .Include(p => p.Staff)
            .Include(p => p.Branch)
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            dbQuery = dbQuery.Where(p => p.BranchId == branchId.Value);
        }

        if (staffId.HasValue && staffId.Value > 0)
        {
            dbQuery = dbQuery.Where(p => p.StaffId == staffId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            dbQuery = dbQuery.Where(p => p.Status.ToUpper() == normalizedStatus);
        }

        var totalCount = await dbQuery.CountAsync(ct);
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

        return (rows, totalCount);
    }

    public async Task<StaffSalaryProfile?> GetSalaryProfileWithRelationsAsync(long id, bool track = false, CancellationToken ct = default)
    {
        var query = _context.StaffSalaryProfiles
            .Include(p => p.Staff)
            .Include(p => p.Branch)
            .AsQueryable();

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Staff?> GetActiveStaffAsync(long staffId, CancellationToken ct = default)
    {
        return await _context.Staffs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == staffId && s.IsDeleted != true, ct);
    }

    public async Task<IReadOnlyList<StaffSalaryProfile>> GetActiveSalaryProfilesForStaffAsync(
        long staffId,
        long? excludeProfileId = null,
        CancellationToken ct = default)
    {
        var query = _context.StaffSalaryProfiles
            .Where(p => p.StaffId == staffId && p.Status == nameof(StaffSalaryProfileStatusCode.ACTIVE));

        if (excludeProfileId.HasValue)
        {
            query = query.Where(p => p.Id != excludeProfileId.Value);
        }

        return await query.ToListAsync(ct);
    }

    public async Task AddSalaryProfileAsync(StaffSalaryProfile profile, CancellationToken ct = default)
    {
        _context.StaffSalaryProfiles.Add(profile);
        await _context.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }

    public Task RemoveSalaryProfileAsync(StaffSalaryProfile profile, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(async token =>
        {
            _context.StaffSalaryProfiles.Remove(profile);
            await _context.SaveChangesAsync(token);
            return true;
        }, ct);
    }

    public async Task<IReadOnlyList<SalaryRequestDto>> GetSalaryRequestsAsync(CancellationToken ct = default)
    {
        if (_sqlQuery == null)
        {
            return Array.Empty<SalaryRequestDto>();
        }

        const string sql = """
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

        var rows = await _sqlQuery.QueryAsync(sql, []);
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

    public Task<bool> HasSalaryRequestCrudSupportAsync() => Task.FromResult(_sqlQuery != null && _sqlCommand != null);

    public async Task<string?> GetSalaryRequestStatusAsync(long requestId, CancellationToken ct = default)
    {
        if (_sqlQuery == null) return null;

        var row = (await _sqlQuery.QueryAsync(
            "SELECT id, status FROM dbo.finance_approval_requests WHERE id = @id",
            [new QueryParam("@id", requestId)])).FirstOrDefault();

        return row == null ? null : Convert.ToString(row.GetValueOrDefault("status")) ?? "";
    }

    public Task ApproveSalaryRequestAsync(
        long requestId,
        long? approverId,
        DateTime now,
        StaffSalaryProfile? createdProfile,
        CancellationToken ct = default)
    {
        if (_sqlCommand == null) return Task.CompletedTask;

        return ExecuteInTransactionAsync(async token =>
        {
            await _sqlCommand.ExecuteAsync(
                "UPDATE dbo.finance_approval_requests SET status = 'APPROVED', approver_id = @approver, updated_at = @now WHERE id = @id",
                [
                    new QueryParam("@approver", (object?)approverId ?? DBNull.Value),
                    new QueryParam("@now", now),
                    new QueryParam("@id", requestId)
                ]);

            if (createdProfile != null)
            {
                _context.StaffSalaryProfiles.Add(createdProfile);
                await _context.SaveChangesAsync(token);
            }

            return true;
        }, ct);
    }

    public async Task RejectSalaryRequestAsync(
        long requestId,
        long? approverId,
        string rejectReason,
        DateTime now,
        CancellationToken ct = default)
    {
        if (_sqlCommand == null) return;

        await _sqlCommand.ExecuteAsync(
            "UPDATE dbo.finance_approval_requests SET status = 'REJECTED', approver_id = @approver, reject_reason = @reason, updated_at = @now WHERE id = @id",
            [
                new QueryParam("@approver", (object?)approverId ?? DBNull.Value),
                new QueryParam("@reason", rejectReason),
                new QueryParam("@now", now),
                new QueryParam("@id", requestId)
            ]);
    }

    public async Task<IReadOnlyList<PayrollRun>> GetPayrollRunsPageAsync(
        long? branchId,
        string? normalizedPeriod,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var dbQuery = _context.PayrollRuns
            .AsNoTracking()
            .Include(r => r.PayrollPeriod)
            .Include(r => r.PayrollItems)
                .ThenInclude(i => i.Staff)
            .Where(r => r.Status != nameof(PayrollRunStatusCode.CANCELED))
            .AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            dbQuery = dbQuery.Where(r => r.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedPeriod))
        {
            dbQuery = dbQuery.Where(r => r.PayrollPeriod.PeriodCode.ToUpper() == normalizedPeriod);
        }

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            dbQuery = dbQuery.Where(r => r.Status.ToUpper() == normalizedStatus);
        }

        return await dbQuery
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<PayrollRun?> GetPayrollRunByIdAsync(long id, bool track = false, CancellationToken ct = default)
    {
        var query = _context.PayrollRuns
            .Include(r => r.PayrollPeriod)
            .Include(r => r.PayrollItems)
                .ThenInclude(i => i.Staff)
            .AsQueryable();

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<Staff>> GetActiveBranchStaffWithTierAsync(long branchId, CancellationToken ct = default)
    {
        return await _context.Staffs
            .AsNoTracking()
            .Include(s => s.StaffTier)
            .Where(s => s.BranchId == branchId && s.IsDeleted != true && (s.Status == null || s.Status != "INACTIVE"))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StaffSalaryProfile>> GetOverlappingSalaryProfilesAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        return await _context.StaffSalaryProfiles
            .AsNoTracking()
            .Where(p => staffIds.Contains(p.StaffId) && p.EffectiveFrom <= endDate && (p.EffectiveTo == null || p.EffectiveTo >= startDate))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StaffContract>> GetActiveContractsForStaffAsync(
        IReadOnlyCollection<long> staffIds,
        CancellationToken ct = default)
    {
        return await _context.StaffContracts
            .AsNoTracking()
            .Where(c => staffIds.Contains(c.StaffId) && c.Status == nameof(StaffContractStatusCode.ACTIVE))
            .ToListAsync(ct);
    }

    public async Task<PayrollPeriod?> GetPayrollPeriodByCodeAsync(string periodCode, CancellationToken ct = default)
    {
        return await _context.PayrollPeriods
            .FirstOrDefaultAsync(p => p.PeriodCode == periodCode, ct);
    }

    public async Task AddPayrollPeriodAsync(PayrollPeriod period, CancellationToken ct = default)
    {
        _context.PayrollPeriods.Add(period);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> GetMaxRunNoAsync(long payrollPeriodId, long branchId, CancellationToken ct = default)
    {
        return await _context.PayrollRuns
            .AsNoTracking()
            .Where(r => r.PayrollPeriodId == payrollPeriodId && r.BranchId == branchId)
            .MaxAsync(r => (int?)r.RunNo, ct) ?? 0;
    }

    public async Task<HashSet<long>> GetValidBranchStaffIdsAsync(
        long branchId,
        IReadOnlyCollection<long> requestedStaffIds,
        CancellationToken ct = default)
    {
        var ids = await _context.Staffs
            .AsNoTracking()
            .Where(s => requestedStaffIds.Contains(s.Id) && s.BranchId == branchId && s.IsDeleted != true)
            .Select(s => s.Id)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    public async Task AddPayrollRunAsync(PayrollRun run, CancellationToken ct = default)
    {
        _context.PayrollRuns.Add(run);
        await _context.SaveChangesAsync(ct);
    }

}
