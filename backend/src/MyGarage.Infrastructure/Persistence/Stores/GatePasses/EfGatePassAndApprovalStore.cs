using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.GatePasses;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.GatePasses;

public sealed class EfGatePassAndApprovalStore : IGatePassAndApprovalStore
{
    private readonly IGarageContext _context;

    public EfGatePassAndApprovalStore(IGarageContext context)
    {
        _context = context;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        try
        {
            return await _context.Database.BeginTransactionAsync();
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

    public async Task<(bool Exists, long? ExecuteBranchId, string? Status)> GetServiceOrderInfoAsync(
        long serviceOrderId,
        CancellationToken ct = default)
    {
        var so = await _context.ServiceOrders
            .AsNoTracking()
            .Where(s => s.Id == serviceOrderId)
            .Select(s => new { s.ExecuteBranchId, s.Status })
            .FirstOrDefaultAsync(ct);

        return so == null
            ? (false, null, null)
            : (true, so.ExecuteBranchId, so.Status);
    }

    public async Task AddGatePassAsync(GatePass entity, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.GatePasses.Add(entity);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<GatePass?> GetGatePassByIdAsync(long gatePassId, CancellationToken ct = default)
    {
        return await _context.GatePasses.FirstOrDefaultAsync(g => g.Id == gatePassId, ct);
    }

    public async Task SaveGatePassAsync(GatePass entity, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task AddApprovalRequestAsync(ApprovalRequest entity, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.ApprovalRequests.Add(entity);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<ApprovalRequest?> GetApprovalRequestByIdAsync(long approvalRequestId, CancellationToken ct = default)
    {
        return await _context.ApprovalRequests.FirstOrDefaultAsync(r => r.Id == approvalRequestId, ct);
    }

    public async Task SaveApprovalDecisionAsync(
        ApprovalRequest entity,
        bool approveLinkedGatePass,
        long approverUserId,
        CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                if (approveLinkedGatePass && entity.GatePassId.HasValue)
                {
                    var gatePass = await _context.GatePasses.FirstOrDefaultAsync(g => g.Id == entity.GatePassId.Value, ct);
                    if (gatePass != null &&
                        !SharedOperationsDomainRules.IsGatePassFinalized(gatePass.Status, gatePass.PassedAt) &&
                        SharedOperationsDomainRules.CanTransitionGatePassStatus(gatePass.Status, SharedOperationsDomainRules.StatusApproved))
                    {
                        gatePass.Status = SharedOperationsDomainRules.StatusApproved;
                        gatePass.ApprovedBy = approverUserId;
                    }
                }

                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }
}
