using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Common;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    /// <summary>
    /// Handles workshop service/package selection, vehicle engine compatibility validation,
    /// service order detail billing synchronization, and insurance asset attachment.
    /// </summary>
    public interface IServiceOrderWorkshopBillingService
    {
        Task<WorkshopServiceSelection> BuildSelectionAsync(
            IEnumerable<long>? serviceIds,
            IEnumerable<long>? packageIds);

        Task EnsureSelectionCompatibleWithVehicleAsync(
            WorkshopServiceSelection selection,
            long vehicleId);

        Task AddInitialServiceDetailsAsync(
            ServiceOrder serviceOrder,
            WorkshopServiceSelection selection,
            long? actorStaffId,
            long? requestedInsuranceCompanyId);

        Task SyncWorkshopServiceDetailsAsync(
            ServiceOrder order,
            WorkshopServiceSelection selection,
            long? actorStaffId,
            long? insuranceCompanyId,
            bool insuranceOrderContext);

        Task<long?> ResolveOrderInsuranceCompanyIdAsync(
            ServiceOrder order,
            long? requestedInsuranceCompanyId);

        Task PersistInsuranceAssetsAsync(
            long serviceOrderId,
            IEnumerable<string>? insuranceCardImageUrls,
            IEnumerable<string>? insuranceDamageImageUrls);

        Task ReplaceInsuranceAssetsAsync(
            long serviceOrderId,
            IEnumerable<string>? insuranceCardImageUrls,
            IEnumerable<string>? insuranceDamageImageUrls);
    }

    public sealed class WorkshopServiceSelection
    {
        public WorkshopServiceSelection(
            List<long> directServiceIds,
            List<long> packageIds,
            List<long> effectiveServiceIds,
            Dictionary<long, StandardService> serviceMap,
            List<Package> packages)
        {
            DirectServiceIds = directServiceIds;
            PackageIds = packageIds;
            EffectiveServiceIds = effectiveServiceIds;
            ServiceMap = serviceMap;
            Packages = packages;
        }

        public static WorkshopServiceSelection Empty { get; } = new(
            new List<long>(),
            new List<long>(),
            new List<long>(),
            new Dictionary<long, StandardService>(),
            new List<Package>());

        public List<long> DirectServiceIds { get; }
        public List<long> PackageIds { get; }
        public List<long> EffectiveServiceIds { get; }
        public Dictionary<long, StandardService> ServiceMap { get; }
        public List<Package> Packages { get; }
        public bool HasSelection => DirectServiceIds.Count > 0 || PackageIds.Count > 0;
        public bool HasInsuranceSignalPackage => Packages.Any(InsuranceBillingRules.IsInsuranceSignalPackage);
        public string? RequestedPackageIds => PackageIds.Count > 0 ? string.Join(",", PackageIds) : null;
    }
}
