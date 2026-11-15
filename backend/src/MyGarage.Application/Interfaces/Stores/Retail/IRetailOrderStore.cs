using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Retail;

public interface IRetailOrderStore
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default);

    Task<bool> ActiveBranchExistsAsync(long branchId, CancellationToken ct = default);

    Task<IReadOnlyList<Product>> GetActiveProductsByIdsAsync(IReadOnlyCollection<long> productIds, CancellationToken ct = default);

    Task<IReadOnlyDictionary<long, int>> GetAvailableStockQuantitiesAsync(
        IReadOnlyCollection<long> productIds,
        long branchId,
        CancellationToken ct = default);

    Task<Voucher?> GetVoucherByCodeAsync(string normalizedCode, CancellationToken ct = default);

    Task<int> CountCustomerRedeemedUsesAsync(long voucherId, long customerId, CancellationToken ct = default);

    Task SaveCheckoutOrderAsync(
        RetailOrder order,
        IReadOnlyList<RetailOrderItem> orderItems,
        VoucherRedemption? redemptionRecord,
        CancellationToken ct = default);

    Task<RetailOrder?> GetOrderWithItemsByIdAsync(long id, CancellationToken ct = default);

    Task<RetailOrder?> GetOrderForMutationAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<RetailOrder>> GetOrdersPageAsync(
        long? branchId,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<RetailCustomerOrderPageData> GetCustomerOrdersPageAsync(
        long customerId,
        long? anchorOrderId,
        int requestedPage,
        int pageSize,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    Task RemoveOrderAsync(RetailOrder order, CancellationToken ct = default);
}

public sealed record RetailCustomerOrderPageData(
    IReadOnlyList<RetailOrder> Orders,
    int TotalCount,
    int Page,
    int DraftCount,
    int ConfirmedCount,
    decimal TotalValue);
