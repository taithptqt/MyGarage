using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed class ServiceOrderVehicleSnapshot
{
    public long VehicleId { get; set; }
    public string? LicensePlate { get; set; }
    public long? BrandId { get; set; }
    public string? BrandName { get; set; }
    public long? ModelId { get; set; }
    public string? ModelName { get; set; }
}

public sealed record ServiceOrderMappingBatchData(
    Dictionary<long, List<ServiceOrderInsuranceAsset>> AssetsByOrderId,
    Dictionary<long, ServiceOrderVehicleSnapshot> VehicleById,
    Dictionary<long, long?> InsuranceCompanyByOrderId,
    Dictionary<long, decimal> DiscountByOrderId);

public sealed record ServiceOrderMappingSingleData(
    List<ServiceOrderInsuranceAsset> Assets,
    ServiceOrderVehicleSnapshot? Vehicle,
    long? InsuranceCompanyId,
    decimal TotalDiscount);

public interface IServiceOrderDataService
{
    Task<(IReadOnlyList<ServiceOrder> Items, int Total)> GetPagedAsync(
        long? scopedBranchId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<AuditLog>> GetEditRequestLogsAsync(
        long serviceOrderId,
        CancellationToken ct = default);

    Task DecideEditRequestAsync(
        AuditLog requestLog,
        AuditLog decisionLog,
        CancellationToken ct = default);

    Task AddInsuranceUploadInTransactionAsync(
        ServiceOrderInsuranceUpload upload,
        CancellationToken ct = default);

    Task<ServiceOrderMappingBatchData> GetBatchMappingDataAsync(
        IReadOnlyCollection<long> orderIds,
        IReadOnlyCollection<long> vehicleIds,
        CancellationToken ct = default);

    Task<ServiceOrderMappingSingleData> GetSingleMappingDataAsync(
        long serviceOrderId,
        long vehicleId,
        CancellationToken ct = default);

    Task<(string RoleCode, string RoleName)> GetStaffRoleInfoAsync(
        long staffId,
        CancellationToken ct = default);
}
