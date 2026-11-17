using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record RetailOrderMinePageResult(
    IReadOnlyList<RetailOrder> Items,
    int Page,
    int PageSize,
    int Total,
    int DraftCount,
    int ConfirmedCount,
    decimal TotalValue);

public sealed record RetailCheckoutResult(
    bool Success,
    string? ErrorMessage,
    object? ErrorPayload,
    RetailOrder? Order);

public sealed record RetailCancelResult(
    bool Success,
    string? ErrorMessage,
    RetailOrder? Order);

public interface IRetailOrderDataService
{
    Task<(IReadOnlyList<RetailOrder> Items, int Total)> GetPagedByBranchAsync(
        long? scopedBranchId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<(IReadOnlyList<RetailOrder> Items, int Total)> GetPagedByCustomerAsync(
        long customerId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<RetailOrderMinePageResult> GetMinePageAsync(
        long customerId,
        int page,
        int pageSize,
        long? anchorId,
        CancellationToken ct = default);

    Task<RetailOrder?> GetWithItemsByIdAsync(long id, CancellationToken ct = default);

    Task<RetailCheckoutResult> CheckoutAsync(
        long customerId,
        long branchId,
        IReadOnlyList<(long ProductId, int Quantity)> normalizedItems,
        string? voucherCode,
        string? customerNotes,
        CancellationToken ct = default);

    Task<RetailCancelResult> CancelOrderAsync(
        long orderId,
        long? customerId,
        bool isStaff,
        string? reason,
        CancellationToken ct = default);

    Task<int> CancelExpiredDraftOrdersAsync(
        TimeSpan timeout,
        CancellationToken ct = default);
}
