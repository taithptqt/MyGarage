using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed partial class EfDiagnosisStore : IDiagnosisStore
{
    private readonly IGarageContext _context;
    private readonly IConfiguration _configuration;

    public EfDiagnosisStore(IGarageContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }

    public Task SaveChangesInTransactionAsync()
    {
        return ExecuteInTransactionAsync(async () =>
        {
            await _context.SaveChangesAsync();
        });
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            await action();
            if (tx != null)
            {
                await tx.CommitAsync();
            }
        });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync();
    }

    public void AddDiagnoses(IEnumerable<Diagnosis> items)
    {
        _context.Diagnoses.AddRange(items);
    }

    public void UpdateDiagnoses(IEnumerable<Diagnosis> items)
    {
        _context.Diagnoses.UpdateRange(items);
    }

    public void UpdateDiagnosis(Diagnosis item)
    {
        _context.Diagnoses.Update(item);
    }

    public void RemoveDiagnoses(IEnumerable<Diagnosis> items)
    {
        _context.Diagnoses.RemoveRange(items);
    }

    public void AddDiagnosisAssignments(IEnumerable<DiagnosisAssignment> items)
    {
        _context.DiagnosisAssignments.AddRange(items);
    }

    public void AddDiagnosisRecommendation(DiagnosisRecommendation item)
    {
        _context.DiagnosisRecommendations.Add(item);
    }

    public void RemoveDiagnosisRecommendations(IEnumerable<DiagnosisRecommendation> items)
    {
        _context.DiagnosisRecommendations.RemoveRange(items);
    }

    public void UpdateServiceOrder(ServiceOrder serviceOrder)
    {
        _context.ServiceOrders.Update(serviceOrder);
    }

    public void AddWorkSlot(WorkSlot slot)
    {
        _context.WorkSlots.Add(slot);
    }

    public void UpdateWorkSlot(WorkSlot slot)
    {
        _context.WorkSlots.Update(slot);
    }

    public void AddServiceOrderSnapshot(ServiceOrderSnapshot snapshot)
    {
        _context.ServiceOrderSnapshots.Add(snapshot);
    }

    public void UpdateServiceOrderSnapshot(ServiceOrderSnapshot snapshot)
    {
        _context.ServiceOrderSnapshots.Update(snapshot);
    }

    public int? GetBranchTeamLeaderWarningMinutes(long branchId)
    {
        return _configuration.GetValue<int?>(
            $"DiagnosisApprovalSla:BranchOverrides:{branchId}:TeamLeaderWarningMinutes");
    }

    public int? GetDefaultTeamLeaderWarningMinutes()
    {
        return _configuration.GetValue<int?>("DiagnosisApprovalSla:TeamLeaderWarningMinutes");
    }

    public int? GetBranchTeamLeaderEscalationMinutes(long branchId)
    {
        return _configuration.GetValue<int?>(
            $"DiagnosisApprovalSla:BranchOverrides:{branchId}:TeamLeaderEscalationMinutes");
    }

    public int? GetDefaultTeamLeaderEscalationMinutes()
    {
        return _configuration.GetValue<int?>("DiagnosisApprovalSla:TeamLeaderEscalationMinutes");
    }

    public IReadOnlyList<string> GetConfiguredDiagnoseBayCodes(long branchId)
    {
        var section = _configuration.GetSection("DiagnosisQueueScheduling:BranchDiagnoseBayCodes");
        if (!section.Exists())
        {
            return Array.Empty<string>();
        }

        var branchSection = section.GetSection(branchId.ToString());
        if (!branchSection.Exists())
        {
            return Array.Empty<string>();
        }

        var rawCodes = branchSection.Get<string[]>() ?? Array.Empty<string>();
        return rawCodes
            .Select(x => (x ?? string.Empty).Trim().ToUpperInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();
    }
}
