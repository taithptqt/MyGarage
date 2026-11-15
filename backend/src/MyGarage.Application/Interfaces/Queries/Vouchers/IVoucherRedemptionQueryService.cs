using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record VoucherRedemptionAdminPageResult(
    IReadOnlyList<VoucherRedemption> Items,
    int Total,
    decimal TotalDiscount);

public sealed record VoucherRedemptionServiceOrderScope(long ExecuteBranchId, long CustomerId, long VehicleId);
public sealed record VoucherRedemptionRetailOrderScope(long BranchId, long? CustomerId);
public sealed record VoucherRedemptionInvoiceScope(
    long? ServiceOrderId,
    long? RetailOrderId,
    long? VehicleId,
    long? ServiceBranch,
    long? RetailBranch,
    long? ServiceCustomer,
    long? RetailCustomer,
    long? ServiceVehicle);

public interface IVoucherRedemptionQueryService
{
    Task<(IReadOnlyList<VoucherRedemption> Items, int Total)> GetPageAsync(
        long? branchId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<VoucherRedemptionAdminPageResult> GetAdminPageAsync(
        long? branchId,
        string? search,
        string? sort,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<VoucherRedemption?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<VoucherRedemption?> FindRedemptionByIdAsync(long id, CancellationToken ct = default);
    Task<VoucherApprovalRequest?> FindApprovalRequestByIdAsync(long approvalRequestId, CancellationToken ct = default);
    Task<VoucherRedemptionServiceOrderScope?> GetServiceOrderScopeAsync(long serviceOrderId, CancellationToken ct = default);
    Task<VoucherRedemptionRetailOrderScope?> GetRetailOrderScopeAsync(long retailOrderId, CancellationToken ct = default);
    Task<VoucherRedemptionInvoiceScope?> GetInvoiceScopeAsync(long invoiceId, CancellationToken ct = default);
}
