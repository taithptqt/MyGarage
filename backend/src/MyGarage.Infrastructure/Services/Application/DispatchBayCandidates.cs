using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Ai;
using MyGarage.Interfaces;

namespace MyGarage.Services;

// Transitional adapter; GarageContext remains the owner of the existing bay model.
public sealed class DispatchBayCandidates(IGarageContext db) : IDispatchBayCandidates
{
    public async Task<IReadOnlyList<DispatchBayCandidate>> GetAvailableAsync(long branchId, CancellationToken cancellationToken = default)
    {
        var query = db.Bays.AsNoTracking()
            .Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted == false);
        // Preserve the original ordinal comparison even on case-insensitive SQL Server collations.
        query = db.Database.IsSqlServer()
            ? query.Where(b => b.OperationalStatus != null && EF.Functions.Collate(b.OperationalStatus + "|", "Latin1_General_100_BIN2") == "AVAILABLE|")
            : query.Where(b => b.OperationalStatus == "AVAILABLE");
        return await query
            .OrderBy(b => b.SequenceOrder).ThenBy(b => b.Id).Take(3)
            .Select(b => new DispatchBayCandidate(b.Id, b.Code, b.Name))
            .ToListAsync(cancellationToken);
    }
}
