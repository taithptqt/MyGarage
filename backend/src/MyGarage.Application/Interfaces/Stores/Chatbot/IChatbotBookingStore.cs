using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Chatbot;

public interface IChatbotBookingStore
{
    Task<Customer?> GetActiveCustomerEntityAsync(long customerId, CancellationToken ct = default);
    Task<List<ChatbotBookingVehicleOptionDto>> GetBookingVehicleOptionsAsync(long customerId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotBookingCategoryRow>> GetActiveServiceCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotBookingServiceCatalogRow>> GetActiveServiceCatalogAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotBookingServiceOptionRow>> GetActiveServicesByCategoryAsync(long categoryId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotBookingBranchRow>> GetNonDeletedBranchesAsync(CancellationToken ct = default);
    Task<Dictionary<long, int>> GetServiceDurationsAsync(IReadOnlyCollection<long> serviceIds, CancellationToken ct = default);
    Task<BranchException?> GetBranchExceptionAsync(long branchId, DateOnly date, CancellationToken ct = default);
    Task<BranchOperatingHour?> GetBranchOperatingHourAsync(long branchId, int dayOfWeek, CancellationToken ct = default);
    Task<Vehicle> CreateOrLinkVehicleWithTransactionAsync(long customerId, string plate, long modelId, string? vin, int? manufactureYear, DateTime utcNow, DateOnly localToday, CancellationToken ct = default);
    Task<bool> VehicleModelExistsAsync(long modelId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotVehicleModelCandidate>> GetVehicleModelCandidatesAsync(CancellationToken ct = default);
    Task<ChatbotVehicleSummary?> GetVehicleSummaryAsync(long vehicleId, CancellationToken ct = default);
    Task<string?> GetBranchNameAsync(long branchId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotBookingServiceSummaryRow>> GetActiveServiceSummariesAsync(IReadOnlyCollection<long> serviceIds, CancellationToken ct = default);
    Task<decimal> GetServicesBasePriceSumAsync(IReadOnlyCollection<long> serviceIds, CancellationToken ct = default);
}
