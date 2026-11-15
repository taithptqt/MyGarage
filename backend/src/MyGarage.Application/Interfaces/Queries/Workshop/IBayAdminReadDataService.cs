using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IBayAdminReadDataService : IBayVehicleLookupDataService
{
    Task<IReadOnlyList<BayBranchLookupItem>> GetLookupBranchesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<BayVehicleLookupItem>> GetLookupVehiclesAsync(int takeVehicles, CancellationToken ct = default);

    Task<bool> BayExistsAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<Bay>> GetBaysWithBranchAsync(bool includeHidden, CancellationToken ct = default);
}
