using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Purchasing.DTOs;

namespace MyGarage.Application.Purchasing;

public interface IPurchaseOrderApplicationService
{
    Task<PurchaseOrderPagedResultDto> GetPurchaseOrdersAsync(
        PurchaseOrderFilterDto filter,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<PurchaseOrderDetailDto?> GetPurchaseOrderByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<PurchaseOrderDetailDto> CreatePurchaseOrderAsync(
        PurchaseOrderCreateInput input,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<PurchaseOrderDetailDto?> UpdatePurchaseOrderAsync(
        long id,
        PurchaseOrderUpdateInput input,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<PurchaseOrderDetailDto> ApprovePurchaseOrderAsync(
        long id,
        PurchaseOrderApproveInput? input,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<PurchaseOrderReceiveResultDto> ReceivePurchaseOrderAsync(
        long id,
        PurchaseOrderReceiveInput input,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<PurchaseOrderReceiveHistoryDto> GetReceiveHistoryAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default);

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

    Task<bool> DeletePurchaseOrderAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default);
}
