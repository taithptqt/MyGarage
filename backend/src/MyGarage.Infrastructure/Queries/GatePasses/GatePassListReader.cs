using Microsoft.EntityFrameworkCore;
using MyGarage.Application.GatePasses;
using MyGarage.Data;

namespace MyGarage.Infrastructure.GatePasses;

public sealed class GatePassListReader(GarageContext db) : IGatePassListReader
{
    public async Task<(IReadOnlyList<GatePassListItem> Items, int Total)> ReadAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.GatePasses.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= total) return (Array.Empty<GatePassListItem>(), total);
        var items = await query.OrderBy(x => x.Id).Skip((int)offset).Take(pageSize)
            .Select(x => new GatePassListItem(x.Id, x.ServiceOrderId, x.ClearanceType, x.QrCode, x.IssuedBy,
                x.ApprovedBy, x.ResolutionNotes, x.OldPartsPackingListUrl, x.IsOldPartsChecked, x.Status,
                x.ScannedBy, x.SecurityCheckedAt, x.PassedAt)).ToListAsync(cancellationToken);
        return (items, total);
    }
}
