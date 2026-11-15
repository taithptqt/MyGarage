namespace MyGarage.Application.Bays;

public interface IBayAdminManagementService
{
    Task<BayAdminCommandResult> CreateBayAsync(BayUpsertRequest? request, long? currentUserId, string contentRootPath, CancellationToken ct = default);

    Task<BayAdminCommandResult> UpdateBayAsync(long id, BayUpsertRequest request, long? currentUserId, string contentRootPath, CancellationToken ct = default);

    Task<BayAdminCommandResult> HideBayAsync(long id, long? currentUserId, CancellationToken ct = default);

    Task<BayAdminCommandResult> LockBayAsync(long id, long? currentUserId, CancellationToken ct = default);

    Task<BayAdminCommandResult> UnlockBayAsync(long id, long? currentUserId, CancellationToken ct = default);

    Task<BayAdminCommandResult> UpdateBayStatusAsync(long id, UpdateBayStatusRequest request, long? currentUserId, string contentRootPath, CancellationToken ct = default);
}
