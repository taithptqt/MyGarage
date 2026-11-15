using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Purchasing.DTOs;

namespace MyGarage.Application.Purchasing;

public interface IPurchaseOrderAnalyticsService
{
    Task<PurchaseOrderFulfillmentDto> GetFulfillmentAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<PurchaseOrderFulfillmentDashboardDto> GetFulfillmentDashboardAsync(
        PurchaseOrderFulfillmentFilterDto filter,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<GrnKpisDto> GetGrnKpisAsync(
        GrnKpiFilterDto filter,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<object> RebuildGrnSnapshotAsync(
        DateOnly? snapshotDate,
        long? branchId,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<object> GetGrnSnapshotTrendAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        long? branchId,
        UserActorContext actor,
        CancellationToken ct = default);
}
