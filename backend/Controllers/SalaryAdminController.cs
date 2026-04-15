using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Services;

namespace MyGarage.Controllers;

[Route("api/salary-admin")]
[ApiController]
[Authorize(Roles = "Staff")]
public class SalaryAdminController : ControllerBase
{
    private const string FinanceApprovalStatusPending = nameof(FinanceApprovalRequestStatusCode.PENDING);
    private const string FinanceApprovalStatusApproved = nameof(FinanceApprovalRequestStatusCode.APPROVED);
    private const string FinanceApprovalStatusRejected = nameof(FinanceApprovalRequestStatusCode.REJECTED);
    private const string StaffSalaryStatusActive = nameof(StaffSalaryProfileStatusCode.ACTIVE);

    private readonly IAccountingCrudService _crud;
    private readonly IGarageContext _db;

    public SalaryAdminController(IAccountingCrudService crud, IGarageContext db)
    {
        _crud = crud;
        _db = db;
    }

    [HttpGet("profiles")]
    public async Task<IActionResult> GetSalaryProfiles([FromQuery] long? branchId = null)
    {
        if (!CanViewSalaryModule()) return Forbid();
        if (!TryResolveAccountingBranchScope(branchId, out var effectiveBranchId, out var denied)) return denied;
        if (!await EnsureTableAsync("staff-salary-profiles", "staff_salary_profiles")) return NotFound();

        var rows = await _crud.QueryAsync(
            """
            SELECT
                p.id,
                p.staff_id,
                s.FullName AS staff_name,
                p.branch_id,
                b.Name AS branch_name,
                p.base_salary,
                p.effective_from,
                p.effective_to,
                p.status,
                p.note,
                p.updated_by,
                p.created_at,
                p.updated_at
            FROM dbo.staff_salary_profiles p
            LEFT JOIN dbo.Staffs s ON s.Id = p.staff_id
            LEFT JOIN dbo.Branches b ON b.Id = p.branch_id
            WHERE (@branchId IS NULL OR p.branch_id = @branchId)
            ORDER BY p.created_at DESC, p.id DESC;
            """,
            [new SqlParameter("@branchId", (object?)effectiveBranchId ?? DBNull.Value)]);

        return Ok(rows);
    }

    [HttpPost("profiles")]
    public async Task<IActionResult> CreateSalaryProfile([FromBody] SalaryProfileUpsertDto request)
    {
        if (!IsAdminStaff()) return Forbid();
        if (!await EnsureTableAsync("staff-salary-profiles", "staff_salary_profiles")) return NotFound();

        var validation = ValidateSalaryProfileRequest(request);
        if (!string.IsNullOrWhiteSpace(validation))
        {
            return BadRequest(new { message = validation });
        }

        var staff = await _db.Staffs.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StaffId && s.IsDeleted != true);
        if (staff is null) return NotFound(new { message = $"Staff #{request.StaffId} not found." });

        var branchId = request.BranchId ?? staff.BranchId;
        var effectiveFrom = DateOnly.FromDateTime(request.EffectiveFrom!.Value.Date);
        var effectiveTo = request.EffectiveTo.HasValue ? DateOnly.FromDateTime(request.EffectiveTo.Value.Date) : (DateOnly?)null;
        var status = NormalizeSalaryProfileStatus(request.Status);
        var currentUserId = ResolveCurrentUserId();

        var createdId = await _crud.ScalarAsync<long?>(
            """
            DECLARE @inserted TABLE (id bigint);
            INSERT INTO dbo.staff_salary_profiles
            (
                staff_id,
                branch_id,
                base_salary,
                effective_from,
                effective_to,
                status,
                note,
                updated_by,
                created_at,
                updated_at
            )
            OUTPUT INSERTED.id INTO @inserted(id)
            VALUES
            (
                @staffId,
                @branchId,
                @baseSalary,
                @effectiveFrom,
                @effectiveTo,
                @status,
                @note,
                @updatedBy,
                SYSDATETIME(),
                SYSDATETIME()
            );
            SELECT TOP 1 id FROM @inserted;
            """,
            [
                new SqlParameter("@staffId", request.StaffId),
                new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value),
                new SqlParameter("@baseSalary", request.BaseSalary!.Value),
                new SqlParameter("@effectiveFrom", effectiveFrom.ToDateTime(TimeOnly.MinValue)),
                new SqlParameter("@effectiveTo", effectiveTo.HasValue ? effectiveTo.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value),
                new SqlParameter("@status", status),
                new SqlParameter("@note", (object?)request.Note?.Trim() ?? DBNull.Value),
                new SqlParameter("@updatedBy", (object?)currentUserId ?? DBNull.Value)
            ]);

        if (!createdId.HasValue)
        {
            return BadRequest(new { message = "Failed to create salary profile." });
        }

        var profile = await GetSalaryProfileByIdAsync(createdId.Value);
        return Ok(profile);
    }

    [HttpPut("profiles/{id:long}")]
    public async Task<IActionResult> UpdateSalaryProfile(long id, [FromBody] SalaryProfileUpsertDto request)
    {
        if (!IsAdminStaff()) return Forbid();
        if (!await EnsureTableAsync("staff-salary-profiles", "staff_salary_profiles")) return NotFound();

        var existing = await GetSalaryProfileByIdAsync(id);
        if (existing is null) return NotFound();

        var validation = ValidateSalaryProfileRequest(request);
        if (!string.IsNullOrWhiteSpace(validation))
        {
            return BadRequest(new { message = validation });
        }

        var staff = await _db.Staffs.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StaffId && s.IsDeleted != true);
        if (staff is null) return NotFound(new { message = $"Staff #{request.StaffId} not found." });

        var branchId = request.BranchId ?? staff.BranchId;
        var effectiveFrom = DateOnly.FromDateTime(request.EffectiveFrom!.Value.Date);
        var effectiveTo = request.EffectiveTo.HasValue ? DateOnly.FromDateTime(request.EffectiveTo.Value.Date) : (DateOnly?)null;
        var status = NormalizeSalaryProfileStatus(request.Status);
        var currentUserId = ResolveCurrentUserId();

        await _crud.ExecuteAsync(
            """
            UPDATE dbo.staff_salary_profiles
            SET
                staff_id = @staffId,
                branch_id = @branchId,
                base_salary = @baseSalary,
                effective_from = @effectiveFrom,
                effective_to = @effectiveTo,
                status = @status,
                note = @note,
                updated_by = @updatedBy,
                updated_at = SYSDATETIME()
            WHERE id = @id;
            """,
            [
                new SqlParameter("@id", id),
                new SqlParameter("@staffId", request.StaffId),
                new SqlParameter("@branchId", (object?)branchId ?? DBNull.Value),
                new SqlParameter("@baseSalary", request.BaseSalary!.Value),
                new SqlParameter("@effectiveFrom", effectiveFrom.ToDateTime(TimeOnly.MinValue)),
                new SqlParameter("@effectiveTo", effectiveTo.HasValue ? effectiveTo.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value),
                new SqlParameter("@status", status),
                new SqlParameter("@note", (object?)request.Note?.Trim() ?? DBNull.Value),
                new SqlParameter("@updatedBy", (object?)currentUserId ?? DBNull.Value)
            ]);

        var profile = await GetSalaryProfileByIdAsync(id);
        return Ok(profile);
    }

    [HttpDelete("profiles/{id:long}")]
    public async Task<IActionResult> DeleteSalaryProfile(long id)
    {
        if (!IsAdminStaff()) return Forbid();
        if (!await EnsureTableAsync("staff-salary-profiles", "staff_salary_profiles")) return NotFound();

        var affected = await _crud.ExecuteAsync(
            "DELETE FROM dbo.staff_salary_profiles WHERE id = @id;",
            [new SqlParameter("@id", id)]);
        if (affected <= 0) return NotFound();

        return Ok(new { message = "Salary profile deleted." });
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetInitialSalaryRequests([FromQuery] string? status = null, [FromQuery] long? branchId = null)
    {
        if (!CanViewSalaryModule()) return Forbid();
        if (!TryResolveAccountingBranchScope(branchId, out var effectiveBranchId, out var denied)) return denied;
        if (!await EnsureTableAsync("finance-approval-requests", "finance_approval_requests")) return NotFound();

        var normalizedStatus = string.IsNullOrWhiteSpace(status) || string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase)
            ? null
            : status.Trim().ToUpperInvariant();

        var rows = await _crud.QueryAsync(
            """
            SELECT
                r.id,
                r.request_type,
                JSON_VALUE(r.request_payload, '$.requestSubtype') AS request_subtype,
                r.status,
                r.amount,
                r.reason,
                r.reject_reason,
                r.request_payload,
                r.created_at,
                r.updated_at,
                r.requested_by,
                req.FullName AS requested_by_name,
                r.approver_id,
                app.FullName AS approver_name,
                TRY_CONVERT(bigint, JSON_VALUE(r.request_payload, '$.staffId')) AS staff_id,
                JSON_VALUE(r.request_payload, '$.staffName') AS staff_name,
                TRY_CONVERT(bigint, JSON_VALUE(r.request_payload, '$.branchId')) AS branch_id,
                br.Name AS branch_name,
                TRY_CONVERT(decimal(18,2), JSON_VALUE(r.request_payload, '$.baseSalary')) AS proposed_base_salary,
                TRY_CONVERT(date, JSON_VALUE(r.request_payload, '$.effectiveFrom')) AS effective_from,
                JSON_VALUE(r.request_payload, '$.note') AS note
            FROM dbo.finance_approval_requests r
            LEFT JOIN dbo.Staffs req ON req.Id = r.requested_by
            LEFT JOIN dbo.Staffs app ON app.Id = r.approver_id
            LEFT JOIN dbo.Branches br ON br.Id = TRY_CONVERT(bigint, JSON_VALUE(r.request_payload, '$.branchId'))
            WHERE r.request_type = 'PAYROLL_ADJUSTMENT'
              AND JSON_VALUE(r.request_payload, '$.requestSubtype') = 'STAFF_INITIAL_SALARY'
              AND (@status IS NULL OR UPPER(r.status) = @status)
              AND (@branchId IS NULL OR TRY_CONVERT(bigint, JSON_VALUE(r.request_payload, '$.branchId')) = @branchId)
            ORDER BY r.created_at DESC, r.id DESC;
            """,
            [
                new SqlParameter("@status", (object?)normalizedStatus ?? DBNull.Value),
                new SqlParameter("@branchId", (object?)effectiveBranchId ?? DBNull.Value)
            ]);

        return Ok(rows);
    }

    [HttpPost("requests/{id:long}/approve")]
    public async Task<IActionResult> ApproveInitialSalaryRequest(long id, [FromBody] SalaryApprovalDecisionDto request)
    {
        if (!IsAdminStaff()) return Forbid();
        if (!await EnsureTableAsync("finance-approval-requests", "finance_approval_requests")) return NotFound();
        if (!await EnsureTableAsync("staff-salary-profiles", "staff_salary_profiles")) return NotFound();

        var row = (await _crud.QueryAsync(
            """
            SELECT TOP 1
                id,
                request_type,
                status,
                request_payload,
                TRY_CONVERT(bigint, JSON_VALUE(request_payload, '$.staffId')) AS staff_id,
                TRY_CONVERT(bigint, JSON_VALUE(request_payload, '$.branchId')) AS branch_id,
                TRY_CONVERT(decimal(18,2), JSON_VALUE(request_payload, '$.baseSalary')) AS proposed_base_salary,
                TRY_CONVERT(date, JSON_VALUE(request_payload, '$.effectiveFrom')) AS effective_from,
                JSON_VALUE(request_payload, '$.note') AS note
            FROM dbo.finance_approval_requests
            WHERE id = @id
              AND request_type = 'PAYROLL_ADJUSTMENT'
              AND JSON_VALUE(request_payload, '$.requestSubtype') = 'STAFF_INITIAL_SALARY';
            """,
            [new SqlParameter("@id", id)])).FirstOrDefault();
        if (row is null) return NotFound();

        var status = Convert.ToString(row.GetValueOrDefault("status")) ?? "";
        if (!string.Equals(status, FinanceApprovalStatusPending, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only pending salary requests can be approved." });
        }

        var payloadStaffId = ToNullableLong(row.GetValueOrDefault("staffId"));
        var payloadBranchId = ToNullableLong(row.GetValueOrDefault("branchId"));
        var payloadBaseSalary = ToNullableDecimal(row.GetValueOrDefault("proposedBaseSalary"));
        var payloadEffectiveFrom = ToNullableDateOnly(row.GetValueOrDefault("effectiveFrom"));
        var payloadNote = Convert.ToString(row.GetValueOrDefault("note"));

        var staffId = request.StaffId ?? payloadStaffId;
        var baseSalary = request.BaseSalary ?? payloadBaseSalary;
        var effectiveFrom = request.EffectiveFrom.HasValue
            ? DateOnly.FromDateTime(request.EffectiveFrom.Value.Date)
            : payloadEffectiveFrom;
        var note = string.IsNullOrWhiteSpace(request.Note) ? payloadNote : request.Note!.Trim();
        var approverId = ResolveCurrentUserId();

        if (!staffId.HasValue || staffId.Value <= 0)
        {
            return BadRequest(new { message = "staffId is missing in approval request payload." });
        }
        if (!baseSalary.HasValue || baseSalary.Value <= 0m)
        {
            return BadRequest(new { message = "baseSalary is missing in approval request payload." });
        }
        if (!effectiveFrom.HasValue)
        {
            return BadRequest(new { message = "effectiveFrom is missing in approval request payload." });
        }

        var staff = await _db.Staffs.AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId.Value && s.IsDeleted != true);
        if (staff is null)
        {
            return NotFound(new { message = $"Staff #{staffId.Value} not found." });
        }

        var profileBranchId = payloadBranchId ?? staff.BranchId;
        var overlapping = (await _crud.QueryAsync(
            """
            SELECT TOP 1 id
            FROM dbo.staff_salary_profiles
            WHERE staff_id = @staffId
              AND effective_from <= @effectiveFrom
              AND (effective_to IS NULL OR effective_to >= @effectiveFrom)
              AND UPPER(ISNULL(status, @staffSalaryStatusActive)) = @staffSalaryStatusActive
              AND (@branchId IS NULL OR branch_id = @branchId OR branch_id IS NULL)
            ORDER BY effective_from DESC, id DESC;
            """,
            [
                new SqlParameter("@staffId", staffId.Value),
                new SqlParameter("@effectiveFrom", effectiveFrom.Value.ToDateTime(TimeOnly.MinValue)),
                new SqlParameter("@branchId", (object?)profileBranchId ?? DBNull.Value),
                new SqlParameter("@staffSalaryStatusActive", StaffSalaryStatusActive)
            ])).FirstOrDefault();
        var overlappingProfileId = ToNullableLong(overlapping?.GetValueOrDefault("id"));
        var updatedExistingProfile = overlappingProfileId.HasValue && overlappingProfileId.Value > 0;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (updatedExistingProfile)
            {
                await _crud.ExecuteAsync(
                    """
                    UPDATE dbo.staff_salary_profiles
                    SET
                        base_salary = @baseSalary,
                        note = @note,
                        updated_by = @updatedBy,
                        updated_at = SYSDATETIME()
                    WHERE id = @profileId;
                    """,
                    [
                        new SqlParameter("@profileId", overlappingProfileId!.Value),
                        new SqlParameter("@baseSalary", baseSalary.Value),
                        new SqlParameter("@note", (object?)note ?? DBNull.Value),
                        new SqlParameter("@updatedBy", (object?)approverId ?? DBNull.Value)
                    ]);
            }
            else
            {
                await _crud.ExecuteAsync(
                    """
                    INSERT INTO dbo.staff_salary_profiles
                    (
                        staff_id,
                        branch_id,
                        base_salary,
                        effective_from,
                        effective_to,
                        status,
                        note,
                        updated_by,
                        created_at,
                        updated_at
                    )
                    VALUES
                    (
                        @staffId,
                        @branchId,
                        @baseSalary,
                        @effectiveFrom,
                        NULL,
                        @staffSalaryStatusActive,
                        @note,
                        @updatedBy,
                        SYSDATETIME(),
                        SYSDATETIME()
                    );
                    """,
                    [
                        new SqlParameter("@staffId", staffId.Value),
                        new SqlParameter("@branchId", (object?)profileBranchId ?? DBNull.Value),
                        new SqlParameter("@baseSalary", baseSalary.Value),
                        new SqlParameter("@effectiveFrom", effectiveFrom.Value.ToDateTime(TimeOnly.MinValue)),
                        new SqlParameter("@staffSalaryStatusActive", StaffSalaryStatusActive),
                        new SqlParameter("@note", (object?)note ?? DBNull.Value),
                        new SqlParameter("@updatedBy", (object?)approverId ?? DBNull.Value)
                    ]);
            }

            await _crud.ExecuteAsync(
                """
                UPDATE dbo.finance_approval_requests
                SET
                    status = @financeApprovalStatusApproved,
                    approver_id = @approverId,
                    reject_reason = NULL,
                    updated_at = SYSDATETIME()
                WHERE id = @id;
                """,
                [
                    new SqlParameter("@id", id),
                    new SqlParameter("@financeApprovalStatusApproved", FinanceApprovalStatusApproved),
                    new SqlParameter("@approverId", (object?)approverId ?? DBNull.Value)
                ]);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return Ok(new
        {
            message = updatedExistingProfile
                ? "Initial salary request approved and active salary profile updated."
                : "Initial salary request approved and salary profile created.",
            requestId = id,
            staffId = staffId.Value,
            baseSalary = baseSalary.Value,
            effectiveFrom = effectiveFrom.Value.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost("requests/{id:long}/reject")]
    public async Task<IActionResult> RejectInitialSalaryRequest(long id, [FromBody] SalaryApprovalDecisionDto request)
    {
        if (!IsAdminStaff()) return Forbid();
        if (!await EnsureTableAsync("finance-approval-requests", "finance_approval_requests")) return NotFound();

        var row = (await _crud.QueryAsync(
            """
            SELECT TOP 1 id, status
            FROM dbo.finance_approval_requests
            WHERE id = @id
              AND request_type = 'PAYROLL_ADJUSTMENT'
              AND JSON_VALUE(request_payload, '$.requestSubtype') = 'STAFF_INITIAL_SALARY';
            """,
            [new SqlParameter("@id", id)])).FirstOrDefault();
        if (row is null) return NotFound();

        var status = Convert.ToString(row.GetValueOrDefault("status")) ?? "";
        if (!string.Equals(status, FinanceApprovalStatusPending, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only pending salary requests can be rejected." });
        }

        var approverId = ResolveCurrentUserId();
        var rejectReason = string.IsNullOrWhiteSpace(request.RejectReason)
            ? (string.IsNullOrWhiteSpace(request.Note) ? "Rejected by admin." : request.Note!.Trim())
            : request.RejectReason!.Trim();

        await _crud.ExecuteAsync(
            """
            UPDATE dbo.finance_approval_requests
            SET
                status = @financeApprovalStatusRejected,
                approver_id = @approverId,
                reject_reason = @rejectReason,
                updated_at = SYSDATETIME()
            WHERE id = @id;
            """,
            [
                new SqlParameter("@id", id),
                new SqlParameter("@financeApprovalStatusRejected", FinanceApprovalStatusRejected),
                new SqlParameter("@approverId", (object?)approverId ?? DBNull.Value),
                new SqlParameter("@rejectReason", rejectReason),
                new SqlParameter("@rejectedStatus", nameof(AccountingStatus.REJECTED))
            ]);

        return Ok(new { message = "Initial salary request rejected.", requestId = id });
    }

    private async Task<Dictionary<string, object?>?> GetSalaryProfileByIdAsync(long id)
    {
        return (await _crud.QueryAsync(
            """
            SELECT TOP 1
                p.id,
                p.staff_id,
                s.FullName AS staff_name,
                p.branch_id,
                b.Name AS branch_name,
                p.base_salary,
                p.effective_from,
                p.effective_to,
                p.status,
                p.note,
                p.updated_by,
                p.created_at,
                p.updated_at
            FROM dbo.staff_salary_profiles p
            LEFT JOIN dbo.Staffs s ON s.Id = p.staff_id
            LEFT JOIN dbo.Branches b ON b.Id = p.branch_id
            WHERE p.id = @id;
            """,
            [new SqlParameter("@id", id)])).FirstOrDefault();
    }

    private async Task<bool> EnsureTableAsync(string configKey, string tableName)
    {
        if (!_crud.TryGetConfig(configKey, out var cfg)) return false;
        return await _crud.TableExistsAsync(cfg);
    }

    private bool IsAdminStaff()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var roleCode = User.FindFirstValue("role_code");
        var roleName = User.FindFirstValue("role_name");
        return string.Equals(roleCode, "ADMIN", StringComparison.OrdinalIgnoreCase)
               || (roleName?.Contains("admin", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private bool IsAccountingStaff()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var roleCode = User.FindFirstValue("role_code");
        var roleName = User.FindFirstValue("role_name");
        var roleNameUpper = (roleName ?? string.Empty).Trim().ToUpperInvariant();

        return string.Equals(roleCode, "ACCOUNTANT", StringComparison.OrdinalIgnoreCase)
               || roleNameUpper.Contains("ACCOUNTANT", StringComparison.OrdinalIgnoreCase)
               || roleNameUpper.Contains("KE TOAN", StringComparison.OrdinalIgnoreCase)
               || roleNameUpper.Contains("KẾ TOÁN", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanViewSalaryModule()
    {
        return IsAdminStaff() || IsAccountingStaff();
    }

    private bool TryResolveAccountingBranchScope(long? requestedBranchId, out long? effectiveBranchId, out ActionResult deniedResult)
    {
        effectiveBranchId = requestedBranchId;
        deniedResult = null!;

        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            deniedResult = Unauthorized();
            return false;
        }

        if (this.IsCurrentAccountingStaff())
        {
            var userBranchId = this.GetCurrentBranchId();
            if (!userBranchId.HasValue)
            {
                deniedResult = Forbid();
                return false;
            }

            if (requestedBranchId.HasValue && requestedBranchId.Value != userBranchId.Value)
            {
                deniedResult = Forbid();
                return false;
            }

            effectiveBranchId = userBranchId.Value;
        }

        return true;
    }

    private long? ResolveCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId) ? userId : null;
    }

    private static string? ValidateSalaryProfileRequest(SalaryProfileUpsertDto request)
    {
        if (request.StaffId <= 0) return "staffId is required.";
        if (!request.BaseSalary.HasValue || request.BaseSalary.Value <= 0m) return "baseSalary must be greater than 0.";
        if (!request.EffectiveFrom.HasValue) return "effectiveFrom is required.";
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value.Date < request.EffectiveFrom.Value.Date)
        {
            return "effectiveTo must be greater than or equal to effectiveFrom.";
        }
        return null;
    }

    private static string NormalizeSalaryProfileStatus(string? status)
    {
        var normalized = (status ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized)) return StaffSalaryStatusActive;
        return Enum.TryParse<StaffSalaryProfileStatusCode>(normalized, true, out var parsed)
            ? parsed.ToString()
            : StaffSalaryStatusActive;
    }

    private static long? ToNullableLong(object? value)
    {
        if (value is null || value == DBNull.Value) return null;
        if (value is long x) return x;
        if (value is int i) return i;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return long.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static decimal? ToNullableDecimal(object? value)
    {
        if (value is null || value == DBNull.Value) return null;
        if (value is decimal d) return d;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static DateOnly? ToNullableDateOnly(object? value)
    {
        if (value is null || value == DBNull.Value) return null;
        if (value is DateOnly dateOnly) return dateOnly;
        if (value is DateTime dateTime) return DateOnly.FromDateTime(dateTime);
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateOnly))
        {
            return parsedDateOnly;
        }
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateTime)
            ? DateOnly.FromDateTime(parsedDateTime)
            : null;
    }

    public sealed class SalaryProfileUpsertDto
    {
        public long StaffId { get; set; }
        public long? BranchId { get; set; }
        public decimal? BaseSalary { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }

    public sealed class SalaryApprovalDecisionDto
    {
        public long? StaffId { get; set; }
        public decimal? BaseSalary { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public string? Note { get; set; }
        public string? RejectReason { get; set; }
    }
}


