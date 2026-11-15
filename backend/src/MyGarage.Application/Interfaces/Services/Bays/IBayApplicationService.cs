using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Bays.DTOs;

namespace MyGarage.Application.Bays;

public interface IBayApplicationService
{
    Task<IReadOnlyList<BayDetailDto>> GetBaysAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<BayDetailDto?> GetBayByIdAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<BayDetailDto> CreateBayAsync(BayCreateInput input, UserActorContext actor, CancellationToken ct = default);
    Task<BayDetailDto?> UpdateBayAsync(long id, BayUpdateInput input, UserActorContext actor, CancellationToken ct = default);
    Task<bool> DeleteBayAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<BayAllocationResultDto> AllocateBayAsync(BayAllocationInput input, UserActorContext actor, CancellationToken ct = default);
    Task<BayAllocationResultDto> ReleaseBayAsync(long bayId, string? notes, UserActorContext actor, CancellationToken ct = default);
    Task<BayAllocationResultDto> TransferBayAsync(long fromBayId, long toBayId, long vehicleId, string? notes, UserActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<BayTimelineItemDto>> GetBayTimelineAsync(DateTime date, long branchId, UserActorContext actor, CancellationToken ct = default);
    Task<BayDetailDto> UpdateBayStatusAsync(long bayId, string status, string? reason, UserActorContext actor, CancellationToken ct = default);
}
