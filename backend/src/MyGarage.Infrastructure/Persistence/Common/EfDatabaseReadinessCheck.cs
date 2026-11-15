using MyGarage.Application.Interfaces.Common;
using MyGarage.Data;

namespace MyGarage.Infrastructure.Persistence.Common;

public sealed class EfDatabaseReadinessCheck(GarageContext db) : IDatabaseReadinessCheck
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        => db.Database.CanConnectAsync(cancellationToken);
}
