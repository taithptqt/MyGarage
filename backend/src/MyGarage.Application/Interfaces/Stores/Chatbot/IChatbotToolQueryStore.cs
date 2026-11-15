using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Interfaces.Stores.Chatbot;

public interface IChatbotToolQueryStore
{
    Task<ChatbotSupportBranch?> ResolveSupportBranchAsync(long? branchId, CancellationToken ct = default);
    Task<ChatbotProductMatch?> FindBestActiveProductAsync(string loweredHint, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotInventoryStockRow>> GetProductInventoryStockAsync(long productId, long? branchId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotToolVehicleRow>> GetCustomerVehiclesForToolAsync(long customerId, bool includeInactiveOwnership, int take, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotToolBranchRow>> GetBranchesForToolAsync(bool onlyActive, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotToolServiceRow>> SearchStandardServicesForToolAsync(string? keyword, int take, CancellationToken ct = default);
    Task<ChatbotCustomerContact?> GetCustomerContactAsync(long customerId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotToolServiceHistoryRow>> GetCustomerServiceHistoryForToolAsync(long customerId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotToolServiceOrderStatusRow>> GetCustomerServiceOrderStatusForToolAsync(long customerId, long? orderId, string? orderCode, int take, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotToolInvoiceStatusRow>> GetCustomerInvoiceStatusForToolAsync(long customerId, long? invoiceId, string? orderCode, int take, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotToolWarrantyRow>> GetCustomerWarrantyHistoryForToolAsync(long customerId, long? vehicleId, int take, CancellationToken ct = default);
    Task<bool> CustomerOwnsVehicleAsync(long customerId, long vehicleId, CancellationToken ct = default);
    Task<IReadOnlyList<long>> GetActiveBranchIdsAsync(IReadOnlyCollection<long> branchIds, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotToolServiceDraftRow>> GetActiveStandardServicesByIdsAsync(IReadOnlyCollection<long> serviceIds, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotToolProductRow>> GetActiveProductsByIdsAsync(IReadOnlyCollection<long> productIds, CancellationToken ct = default);
    Task<Dictionary<long, decimal>> GetAvailableStockByProductIdsAsync(long branchId, IReadOnlyCollection<long> productIds, CancellationToken ct = default);
}
