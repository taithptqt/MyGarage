using MyGarage.Models;

namespace MyGarage.Application.Bays;

public interface IBayCommandStore
{
    Task AddBayAsync(Bay bay, CancellationToken ct = default);
    Task UpdateBayAndStatusLogAsync(BayStatusLog? statusLog, CancellationToken ct = default);
    Task SoftDeleteBayAsync(Bay bay, CancellationToken ct = default);
    Task UpdateBayStatusInTransactionAsync(Bay bay, BayStatusLog statusLog, string normalizedTarget, DateTime now, CancellationToken ct = default);
}
