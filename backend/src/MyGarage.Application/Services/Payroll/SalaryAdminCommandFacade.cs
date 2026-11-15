using System.Globalization;
using MyGarage.Application.Common;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Enums;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Application.Payroll;

public sealed class SalaryAdminCommandFacade : ISalaryAdminCommandFacade
{
    private const string FinanceApprovalStatusPending = nameof(FinanceApprovalRequestStatusCode.PENDING);
    private const string FinanceApprovalStatusRejected = nameof(FinanceApprovalRequestStatusCode.REJECTED);
    private const string StaffSalaryStatusActive = nameof(StaffSalaryProfileStatusCode.ACTIVE);

    private readonly IAccountingSchemaService _schema;
    private readonly IAccountingSqlQueryService _sqlQuery;
    private readonly IAccountingSqlCommandService _sqlCommand;
    private readonly ISalaryAdminDataService _dataService;
    private readonly SalaryProfileListQuery _salaryProfiles;

    public SalaryAdminCommandFacade(
        IAccountingSchemaService schema,
        IAccountingSqlQueryService sqlQuery,
        IAccountingSqlCommandService sqlCommand,
        ISalaryAdminDataService dataService,
        SalaryProfileListQuery salaryProfiles)
    {
        _schema = schema;
        _sqlQuery = sqlQuery;
        _sqlCommand = sqlCommand;
        _dataService = dataService;
        _salaryProfiles = salaryProfiles;
    }

    public async Task<SalaryAdminCommandResult> GetSalaryProfilesAsync(
        long? branchId,
        int? page,
        int? pageSize,
        SalaryAdminActorContext actor,
        CancellationToken ct = default)
    {
        if (!CanViewSalaryModule(actor)) return SalaryAdminCommandResult.Forbid();
        if (!await EnsureTableAsync("staff-salary-profiles")) return SalaryAdminCommandResult.NotFound();

        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize);
        var result = await _salaryProfiles.ExecuteAsync(branchId, currentPage, size, ct);
        return SalaryAdminCommandResult.Ok(
            PagedEnvelope<Dictionary<string, object?>>.Create(result.Items, result.Total ?? result.Items.Count, currentPage, size));
    }

    public async Task<SalaryAdminCommandResult> CreateSalaryProfileAsync(
        SalaryProfileUpsertDto request,
        SalaryAdminActorContext actor,
        CancellationToken ct = default)
    {
        if (!IsAdminStaff(actor)) return SalaryAdminCommandResult.Forbid();
        if (!await EnsureTableAsync("staff-salary-profiles")) return SalaryAdminCommandResult.NotFound();

        var validation = ValidateSalaryProfileRequest(request);
        if (!string.IsNullOrWhiteSpace(validation))
        {
            return SalaryAdminCommandResult.BadRequest(validation);
        }

        var (staffFound, staffBranchId) = await _dataService.GetActiveStaffAsync(request.StaffId, ct);
        if (!staffFound) return SalaryAdminCommandResult.NotFound($"Staff #{request.StaffId} not found.");

        var parameters = BuildSalaryProfileWriteParameters(request, staffBranchId, actor.UserId);
        var createdId = await _sqlQuery.ScalarAsync<long?>(
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
            parameters);

        if (!createdId.HasValue)
        {
            return SalaryAdminCommandResult.BadRequest("Failed to create salary profile.");
        }

        var profile = await GetSalaryProfileByIdAsync(createdId.Value);
        return SalaryAdminCommandResult.Ok(profile);
    }

    public async Task<SalaryAdminCommandResult> UpdateSalaryProfileAsync(
        long id,
        SalaryProfileUpsertDto request,
        SalaryAdminActorContext actor,
        CancellationToken ct = default)
    {
        if (!IsAdminStaff(actor)) return SalaryAdminCommandResult.Forbid();
        if (!await EnsureTableAsync("staff-salary-profiles")) return SalaryAdminCommandResult.NotFound();

        var existing = await GetSalaryProfileByIdAsync(id);
        if (existing is null) return SalaryAdminCommandResult.NotFound();

        var validation = ValidateSalaryProfileRequest(request);
        if (!string.IsNullOrWhiteSpace(validation))
        {
            return SalaryAdminCommandResult.BadRequest(validation);
        }

        var (staffFound, staffBranchId) = await _dataService.GetActiveStaffAsync(request.StaffId, ct);
        if (!staffFound) return SalaryAdminCommandResult.NotFound($"Staff #{request.StaffId} not found.");

        var parameters = BuildSalaryProfileWriteParameters(request, staffBranchId, actor.UserId, id);
        await _sqlCommand.ExecuteAsync(
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
            parameters);

        var profile = await GetSalaryProfileByIdAsync(id);
        return SalaryAdminCommandResult.Ok(profile);
    }

    public async Task<SalaryAdminCommandResult> DeleteSalaryProfileAsync(
        long id,
        SalaryAdminActorContext actor,
        CancellationToken ct = default)
    {
        if (!IsAdminStaff(actor)) return SalaryAdminCommandResult.Forbid();
        if (!await EnsureTableAsync("staff-salary-profiles")) return SalaryAdminCommandResult.NotFound();

        var affected = await _sqlCommand.ExecuteAsync(
            "DELETE FROM dbo.staff_salary_profiles WHERE id = @id;",
            [new QueryParam("@id", id)]);
        if (affected <= 0) return SalaryAdminCommandResult.NotFound();

        return SalaryAdminCommandResult.Ok(new { message = "Salary profile deleted." });
    }

    public async Task<SalaryAdminCommandResult> GetInitialSalaryRequestsAsync(
        string? status,
        long? branchId,
        int? page,
        int? pageSize,
        SalaryAdminActorContext actor,
        CancellationToken ct = default)
    {
        if (!CanViewSalaryModule(actor)) return SalaryAdminCommandResult.Forbid();
        if (!await EnsureTableAsync("finance-approval-requests")) return SalaryAdminCommandResult.NotFound();

        var normalizedStatus = string.IsNullOrWhiteSpace(status) || string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase)
            ? null
            : status.Trim().ToUpperInvariant();

        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize);
        var offset = ((long)currentPage - 1) * size;
        var where = """
            WHERE r.request_type = 'PAYROLL_ADJUSTMENT'
              AND JSON_VALUE(r.request_payload, '$.requestSubtype') = 'STAFF_INITIAL_SALARY'
              AND (@status IS NULL OR UPPER(r.status) = @status)
              AND (@branchId IS NULL OR TRY_CONVERT(bigint, JSON_VALUE(r.request_payload, '$.branchId')) = @branchId)
            """;

        List<QueryParam> FilterParameters() =>
        [
            new QueryParam("@status", (object?)normalizedStatus ?? DBNull.Value),
            new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)
        ];

        var totals = await _sqlQuery.QueryAsync("""
            SELECT COUNT(*) AS total,
                COALESCE(SUM(CASE WHEN UPPER(r.status) + '|' = 'PENDING|' THEN 1 ELSE 0 END), 0) AS pending_count,
                COALESCE(SUM(CASE WHEN UPPER(r.status) + '|' = 'APPROVED|' THEN 1 ELSE 0 END), 0) AS approved_count
            FROM dbo.finance_approval_requests r
            """ + " " + where, FilterParameters());

        var summary = totals.Single();
        var total = Convert.ToInt32(summary["total"]);

        IReadOnlyList<Dictionary<string, object?>> rows;
        if (offset >= total)
        {
            rows = [];
        }
        else
        {
            var sql = """
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
                """ + " " + where + " ORDER BY r.created_at DESC, r.id DESC OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY";

            var parameters = FilterParameters();
            parameters.Add(new QueryParam("@offset", offset));
            parameters.Add(new QueryParam("@size", size));
            rows = await _sqlQuery.QueryAsync(sql, parameters);
        }

        var envelope = PagedEnvelope<Dictionary<string, object?>>.Create(rows, total, currentPage, size);
        return SalaryAdminCommandResult.Ok(new
        {
            items = envelope.Items,
            total = envelope.Total,
            totalCount = envelope.TotalCount,
            page = envelope.Page,
            pageSize = envelope.PageSize,
            totalPages = envelope.TotalPages,
            hasPreviousPage = envelope.HasPreviousPage,
            hasNextPage = envelope.HasNextPage,
            pendingCount = Convert.ToInt32(summary["pendingCount"]),
            approvedCount = Convert.ToInt32(summary["approvedCount"])
        });
    }

    public async Task<SalaryAdminCommandResult> ApproveInitialSalaryRequestAsync(
        long id,
        SalaryApprovalDecisionDto request,
        SalaryAdminActorContext actor,
        CancellationToken ct = default)
    {
        if (!IsAdminStaff(actor)) return SalaryAdminCommandResult.Forbid();
        if (!await EnsureTableAsync("finance-approval-requests")) return SalaryAdminCommandResult.NotFound();
        if (!await EnsureTableAsync("staff-salary-profiles")) return SalaryAdminCommandResult.NotFound();

        var row = (await _sqlQuery.QueryAsync(
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
            [new QueryParam("@id", id)])).FirstOrDefault();
        if (row is null) return SalaryAdminCommandResult.NotFound();

        var status = Convert.ToString(row.GetValueOrDefault("status")) ?? "";
        if (!string.Equals(status, FinanceApprovalStatusPending, StringComparison.OrdinalIgnoreCase))
        {
            return SalaryAdminCommandResult.BadRequest("Only pending salary requests can be approved.");
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

        if (!staffId.HasValue || staffId.Value <= 0)
        {
            return SalaryAdminCommandResult.BadRequest("staffId is missing in approval request payload.");
        }
        if (!baseSalary.HasValue || baseSalary.Value <= 0m)
        {
            return SalaryAdminCommandResult.BadRequest("baseSalary is missing in approval request payload.");
        }
        if (!effectiveFrom.HasValue)
        {
            return SalaryAdminCommandResult.BadRequest("effectiveFrom is missing in approval request payload.");
        }

        var (staffFound, staffBranchId) = await _dataService.GetActiveStaffAsync(staffId.Value, ct);
        if (!staffFound)
        {
            return SalaryAdminCommandResult.NotFound($"Staff #{staffId.Value} not found.");
        }

        var profileBranchId = payloadBranchId ?? staffBranchId;
        var overlapping = (await _sqlQuery.QueryAsync(
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
                new QueryParam("@staffId", staffId.Value),
                new QueryParam("@effectiveFrom", effectiveFrom.Value.ToDateTime(TimeOnly.MinValue)),
                new QueryParam("@branchId", (object?)profileBranchId ?? DBNull.Value),
                new QueryParam("@staffSalaryStatusActive", StaffSalaryStatusActive)
            ])).FirstOrDefault();
        if (overlapping is not null)
        {
            return SalaryAdminCommandResult.Conflict("Staff already has an active salary profile for the selected effective date.");
        }

        await _dataService.ApproveInitialSalaryRequestInTransactionAsync(
            id,
            staffId.Value,
            profileBranchId,
            baseSalary.Value,
            effectiveFrom.Value,
            note,
            actor.UserId,
            ct);

        return SalaryAdminCommandResult.Ok(new
        {
            message = "Initial salary request approved and salary profile created.",
            requestId = id,
            staffId = staffId.Value,
            baseSalary = baseSalary.Value,
            effectiveFrom = effectiveFrom.Value.ToString("yyyy-MM-dd")
        });
    }

    public async Task<SalaryAdminCommandResult> RejectInitialSalaryRequestAsync(
        long id,
        SalaryApprovalDecisionDto request,
        SalaryAdminActorContext actor,
        CancellationToken ct = default)
    {
        if (!IsAdminStaff(actor)) return SalaryAdminCommandResult.Forbid();
        if (!await EnsureTableAsync("finance-approval-requests")) return SalaryAdminCommandResult.NotFound();

        var row = (await _sqlQuery.QueryAsync(
            """
            SELECT TOP 1 id, status
            FROM dbo.finance_approval_requests
            WHERE id = @id
              AND request_type = 'PAYROLL_ADJUSTMENT'
              AND JSON_VALUE(request_payload, '$.requestSubtype') = 'STAFF_INITIAL_SALARY';
            """,
            [new QueryParam("@id", id)])).FirstOrDefault();
        if (row is null) return SalaryAdminCommandResult.NotFound();

        var status = Convert.ToString(row.GetValueOrDefault("status")) ?? "";
        if (!string.Equals(status, FinanceApprovalStatusPending, StringComparison.OrdinalIgnoreCase))
        {
            return SalaryAdminCommandResult.BadRequest("Only pending salary requests can be rejected.");
        }

        var rejectReason = string.IsNullOrWhiteSpace(request.RejectReason)
            ? (string.IsNullOrWhiteSpace(request.Note) ? "Rejected by admin." : request.Note!.Trim())
            : request.RejectReason!.Trim();

        await _sqlCommand.ExecuteAsync(
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
                new QueryParam("@id", id),
                new QueryParam("@financeApprovalStatusRejected", FinanceApprovalStatusRejected),
                new QueryParam("@approverId", (object?)actor.UserId ?? DBNull.Value),
                new QueryParam("@rejectReason", rejectReason),
                new QueryParam("@rejectedStatus", nameof(AccountingStatus.REJECTED))
            ]);

        return SalaryAdminCommandResult.Ok(new { message = "Initial salary request rejected.", requestId = id });
    }

    private List<QueryParam> BuildSalaryProfileWriteParameters(
        SalaryProfileUpsertDto request,
        long? staffBranchId,
        long? currentUserId,
        long? id = null)
    {
        var branchId = request.BranchId ?? staffBranchId;
        var effectiveFrom = DateOnly.FromDateTime(request.EffectiveFrom!.Value.Date);
        var effectiveTo = request.EffectiveTo.HasValue ? DateOnly.FromDateTime(request.EffectiveTo.Value.Date) : (DateOnly?)null;
        var status = NormalizeSalaryProfileStatus(request.Status);

        var parameters = new List<QueryParam>
        {
            new("@staffId", request.StaffId),
            new("@branchId", (object?)branchId ?? DBNull.Value),
            new("@baseSalary", request.BaseSalary!.Value),
            new("@effectiveFrom", effectiveFrom.ToDateTime(TimeOnly.MinValue)),
            new("@effectiveTo", effectiveTo.HasValue ? effectiveTo.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value),
            new("@status", status),
            new("@note", (object?)request.Note?.Trim() ?? DBNull.Value),
            new("@updatedBy", (object?)currentUserId ?? DBNull.Value)
        };

        if (id.HasValue)
        {
            parameters.Insert(0, new QueryParam("@id", id.Value));
        }

        return parameters;
    }

    private async Task<Dictionary<string, object?>?> GetSalaryProfileByIdAsync(long id)
    {
        return (await _sqlQuery.QueryAsync(
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
            [new QueryParam("@id", id)])).FirstOrDefault();
    }

    private async Task<bool> EnsureTableAsync(string configKey)
    {
        if (!_schema.TryGetConfig(configKey, out var cfg)) return false;
        return await _schema.TableExistsAsync(cfg);
    }

    private static bool IsAdminStaff(SalaryAdminActorContext actor)
    {
        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(actor.RoleCode, "ADMIN", StringComparison.OrdinalIgnoreCase)
               || (actor.RoleName?.Contains("admin", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool IsAccountingStaff(SalaryAdminActorContext actor)
    {
        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var roleNameUpper = (actor.RoleName ?? string.Empty).Trim().ToUpperInvariant();
        return string.Equals(actor.RoleCode, "ACCOUNTANT", StringComparison.OrdinalIgnoreCase)
               || roleNameUpper.Contains("ACCOUNTANT", StringComparison.OrdinalIgnoreCase)
               || roleNameUpper.Contains("KE TOAN", StringComparison.OrdinalIgnoreCase)
               || roleNameUpper.Contains("KẾ TOÁN", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanViewSalaryModule(SalaryAdminActorContext actor)
        => IsAdminStaff(actor) || IsAccountingStaff(actor);

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
}
