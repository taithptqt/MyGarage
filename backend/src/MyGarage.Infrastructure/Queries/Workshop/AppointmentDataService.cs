using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfAppointmentDataService(
    IGarageContext db,
    IAuditLogService auditLogService) : IAppointmentDataService
{
    public async Task<(bool Found, long CustomerId, long BranchId)> GetScopeMetaAsync(long id, CancellationToken ct = default)
    {
        var scopeMeta = await db.Appointments
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.CustomerId, a.BranchId })
            .FirstOrDefaultAsync(ct);

        return scopeMeta is null ? (false, 0, 0) : (true, scopeMeta.CustomerId, scopeMeta.BranchId);
    }

    public Task<List<AuditLog>> GetEditRequestLogsAsync(long appointmentId, CancellationToken ct = default) =>
        db.AuditLogs
            .AsNoTracking()
            .Where(log =>
                log.Action == "REQUEST_EDIT" &&
                log.TableName == "APPOINTMENTS" &&
                log.RecordId == appointmentId)
            .OrderByDescending(log => log.Id)
            .Take(100)
            .ToListAsync(ct);

    public async Task DecideEditRequestInTransactionAsync(
        AuditLog requestLog,
        string? beforeDecision,
        long actorUserId,
        DateTime now,
        CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await TryBeginTransactionAsync(ct);
            try
            {
                await auditLogService.UpdateAsync(requestLog.Id, requestLog);

                await auditLogService.AddAsync(new AuditLog
                {
                    UserId = actorUserId,
                    UserType = "STAFF",
                    BranchId = requestLog.BranchId,
                    Action = "DECIDE_EDIT_REQUEST",
                    TableName = requestLog.TableName,
                    RecordId = requestLog.RecordId,
                    OldValue = beforeDecision,
                    NewValue = requestLog.NewValue,
                    Timestamp = now
                });

                if (transaction != null) await transaction.CommitAsync(ct);
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task AddInsuranceUploadAsync(AppointmentInsuranceUpload upload, CancellationToken ct = default)
    {
        db.AppointmentInsuranceUploads.Add(upload);
        await SaveChangesInTransactionAsync(ct);
    }

    public async Task<(string RoleCode, string RoleName)> GetStaffRoleInfoAsync(long staffId, CancellationToken ct = default)
    {
        var roleInfo = await (
            from staff in db.Staffs.AsNoTracking()
            join role in db.Roles.AsNoTracking() on staff.RoleId equals role.Id into roleJoin
            from role in roleJoin.DefaultIfEmpty()
            where staff.Id == staffId
            select new
            {
                role.RoleCode,
                role.RoleName
            }).FirstOrDefaultAsync(ct);

        return (roleInfo?.RoleCode ?? string.Empty, roleInfo?.RoleName ?? string.Empty);
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await db.Database.BeginTransactionAsync(ct);
    }

    private async Task SaveChangesInTransactionAsync(CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await TryBeginTransactionAsync(ct);
            await db.SaveChangesAsync(ct);
            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
            }
        });
    }
}
