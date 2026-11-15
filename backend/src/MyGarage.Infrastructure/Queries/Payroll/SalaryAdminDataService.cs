using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Common;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfSalaryAdminDataService(
    IGarageContext db,
    IAccountingSqlCommandService crud) : ISalaryAdminDataService
{
    private const string FinanceApprovalStatusApproved = nameof(FinanceApprovalRequestStatusCode.APPROVED);
    private const string StaffSalaryStatusActive = nameof(StaffSalaryProfileStatusCode.ACTIVE);

    public async Task<(bool Found, long? BranchId)> GetActiveStaffAsync(long staffId, CancellationToken ct = default)
    {
        var staff = await db.Staffs
            .AsNoTracking()
            .Where(s => s.Id == staffId && s.IsDeleted != true)
            .Select(s => new { s.BranchId })
            .FirstOrDefaultAsync(ct);

        return staff is null ? (false, null) : (true, staff.BranchId);
    }

    public async Task ApproveInitialSalaryRequestInTransactionAsync(
        long requestId,
        long staffId,
        long? profileBranchId,
        decimal baseSalary,
        DateOnly effectiveFrom,
        string? note,
        long? approverId,
        CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(ct);
            try
            {
                await crud.ExecuteAsync(
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
                        new QueryParam("@staffId", staffId),
                        new QueryParam("@branchId", (object?)profileBranchId ?? DBNull.Value),
                        new QueryParam("@baseSalary", baseSalary),
                        new QueryParam("@effectiveFrom", effectiveFrom.ToDateTime(TimeOnly.MinValue)),
                        new QueryParam("@staffSalaryStatusActive", StaffSalaryStatusActive),
                        new QueryParam("@note", (object?)note ?? DBNull.Value),
                        new QueryParam("@updatedBy", (object?)approverId ?? DBNull.Value),
                        new QueryParam("@activeStatus", nameof(AccountingStatus.ACTIVE))
                    ]);

                await crud.ExecuteAsync(
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
                        new QueryParam("@id", requestId),
                        new QueryParam("@financeApprovalStatusApproved", FinanceApprovalStatusApproved),
                        new QueryParam("@approverId", (object?)approverId ?? DBNull.Value)
                    ]);

                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await db.Database.BeginTransactionAsync(ct);
    }
}
