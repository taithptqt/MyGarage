using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Constants;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Appointments;

public sealed partial class EfAppointmentStore
{
    public async Task<List<StandardServiceLookupItem>> GetActiveStandardServiceLookupsAsync()
    {
        return await _context.StandardServices
            .AsNoTracking()
            .Where(s => s.IsDeleted != true && s.IsActive == true)
            .Select(s => new StandardServiceLookupItem(s.Id, s.Code, s.Name, s.StandardMinutes))
            .ToListAsync();
    }

    public async Task<int?> GetStandardServiceMinutesAsync(long serviceId)
    {
        return await _context.StandardServices
            .AsNoTracking()
            .Where(s => s.Id == serviceId)
            .Select(s => (int?)s.StandardMinutes)
            .FirstOrDefaultAsync();
    }

    public async Task<List<StandardService>> GetActiveStandardServicesByIdsAsync(
        IReadOnlyCollection<long> ids,
        bool includeCategory = false)
    {
        if (ids.Count == 0)
        {
            return new List<StandardService>();
        }

        IQueryable<StandardService> query = _context.StandardServices.AsNoTracking();
        if (includeCategory)
        {
            query = query.Include(s => s.Category);
        }

        return await query
            .Where(s => ids.Contains(s.Id) && s.IsDeleted != true && s.IsActive == true)
            .ToListAsync();
    }

    public async Task<List<StandardServiceLookupItem>> GetActiveStandardServiceLookupsByIdsAsync(IReadOnlyCollection<long> ids)
    {
        if (ids.Count == 0)
        {
            return new List<StandardServiceLookupItem>();
        }

        return await _context.StandardServices
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id) && s.IsDeleted != true && s.IsActive == true)
            .Select(s => new StandardServiceLookupItem(s.Id, s.Code, s.Name, s.StandardMinutes))
            .ToListAsync();
    }

    public async Task<Dictionary<long, StandardService>> GetActiveStandardServiceMapAsync(IReadOnlyCollection<long> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<long, StandardService>();
        }

        return await _context.StandardServices
            .AsNoTracking()
            .Where(s =>
                ids.Contains(s.Id) &&
                s.IsDeleted != true &&
                s.IsActive != false)
            .ToDictionaryAsync(s => s.Id);
    }

    public async Task<Dictionary<long, string>> GetStandardServiceNamesByIdsAsync(IReadOnlyCollection<long> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        return await _context.StandardServices
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ToDictionaryAsync(s => s.Id, s => s.Name);
    }

    public async Task<int> CountActivePackagesByIdsAsync(IReadOnlyCollection<long> packageIds)
    {
        if (packageIds.Count == 0)
        {
            return 0;
        }

        return await _context.Packages
            .AsNoTracking()
            .CountAsync(package => packageIds.Contains(package.Id) && package.IsDeleted != true && package.IsActive != false);
    }

    public async Task<List<Package>> GetActivePackagesByIdsAsync(
        IReadOnlyCollection<long> packageIds,
        bool includeCategoryAndItems = true)
    {
        if (packageIds.Count == 0)
        {
            return new List<Package>();
        }

        IQueryable<Package> query = _context.Packages.AsNoTracking();
        if (includeCategoryAndItems)
        {
            query = query
                .Include(package => package.Category)
                .Include(package => package.PackageItems.Where(item => item.IsDeleted != true && item.IsActive != false))
                    .ThenInclude(item => item.StandardService)
                        .ThenInclude(service => service.Category);
        }
        else
        {
            query = query
                .Include(package => package.PackageItems.Where(item => item.IsDeleted != true && item.IsActive != false))
                    .ThenInclude(item => item.StandardService);
        }

        return await query
            .Where(package => packageIds.Contains(package.Id) && package.IsDeleted != true && package.IsActive != false)
            .ToListAsync();
    }

    public async Task<List<long>> GetPackageItemServiceIdsAsync(IReadOnlyCollection<long> packageIds)
    {
        if (packageIds.Count == 0)
        {
            return new List<long>();
        }

        return await _context.PackageItems
            .AsNoTracking()
            .Where(item =>
                packageIds.Contains(item.PackageId) &&
                item.IsDeleted != true &&
                item.IsActive != false &&
                item.StandardService.IsDeleted != true &&
                item.StandardService.IsActive != false)
            .Select(item => item.StandardServiceId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<Dictionary<long, List<long>>> GetPackageItemServiceIdsByPackageIdsAsync(IReadOnlyCollection<long> packageIds)
    {
        if (packageIds.Count == 0)
        {
            return new Dictionary<long, List<long>>();
        }

        return await _context.PackageItems
            .AsNoTracking()
            .Where(item =>
                packageIds.Contains(item.PackageId) &&
                item.IsDeleted != true &&
                item.IsActive != false &&
                item.StandardService.IsDeleted != true &&
                item.StandardService.IsActive != false)
            .GroupBy(item => item.PackageId)
            .ToDictionaryAsync(
                group => group.Key,
                group => group.Select(item => item.StandardServiceId).Distinct().ToList());
    }

    public async Task<Dictionary<long, string>> GetPackageNamesByIdsAsync(IReadOnlyCollection<long> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        return await _context.Packages
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .OrderBy(p => p.Name)
            .ToDictionaryAsync(p => p.Id, p => p.Name);
    }

    public async Task<List<PartDemandSnapshot>> GetMandatoryPartDemandsAsync(long vehicleModelId, IReadOnlyCollection<long> serviceIds)
    {
        if (vehicleModelId <= 0 || serviceIds.Count == 0)
        {
            return new List<PartDemandSnapshot>();
        }

        return await _context.ServiceBoms
            .AsNoTracking()
            .Where(x =>
                x.VehicleModelId == vehicleModelId &&
                serviceIds.Contains(x.ServiceId) &&
                x.IsMandatory)
            .GroupBy(x => x.ProductId)
            .Select(g => new PartDemandSnapshot(g.Key, g.Sum(x => x.Quantity)))
            .ToListAsync();
    }

    public async Task<List<MandatoryPartBomItem>> GetMandatoryServiceBomsAsync(
        IReadOnlyCollection<long> vehicleModelIds,
        IReadOnlyCollection<long> serviceIds)
    {
        if (vehicleModelIds.Count == 0 || serviceIds.Count == 0)
        {
            return new List<MandatoryPartBomItem>();
        }

        return await _context.ServiceBoms
            .AsNoTracking()
            .Where(bom =>
                vehicleModelIds.Contains(bom.VehicleModelId) &&
                serviceIds.Contains(bom.ServiceId) &&
                bom.IsMandatory)
            .Select(bom => new MandatoryPartBomItem(
                bom.VehicleModelId,
                bom.ServiceId,
                bom.Service.Name,
                bom.ProductId,
                bom.Product.Name,
                bom.Product.Sku,
                bom.Quantity,
                bom.Unit))
            .ToListAsync();
    }

    public async Task<Dictionary<long, decimal>> GetAvailableInventoryByProductAsync(
        long branchId,
        IReadOnlyCollection<long> productIds)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<long, decimal>();
        }

        return await _context.Inventories
            .AsNoTracking()
            .Where(i =>
                productIds.Contains(i.ProductId) &&
                i.Warehouse.BranchId == branchId &&
                i.Warehouse.IsDeleted != true &&
                i.Warehouse.IsActive != false)
            .GroupBy(i => i.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                AvailableQuantity = g.Sum(i => i.Quantity - (i.ReservedQuantity ?? 0m))
            })
            .ToDictionaryAsync(x => x.ProductId, x => x.AvailableQuantity);
    }

    public async Task<Dictionary<(long BranchId, long ProductId), decimal>> GetAvailableInventoryByBranchAndProductAsync(
        IReadOnlyCollection<long> branchIds,
        IReadOnlyCollection<long> productIds)
    {
        if (branchIds.Count == 0 || productIds.Count == 0)
        {
            return new Dictionary<(long BranchId, long ProductId), decimal>();
        }

        return await _context.Inventories
            .AsNoTracking()
            .Where(inventory =>
                productIds.Contains(inventory.ProductId) &&
                branchIds.Contains(inventory.Warehouse.BranchId) &&
                inventory.Warehouse.IsDeleted != true &&
                inventory.Warehouse.IsActive != false)
            .GroupBy(inventory => new { inventory.Warehouse.BranchId, inventory.ProductId })
            .ToDictionaryAsync(
                group => (group.Key.BranchId, group.Key.ProductId),
                group => group.Sum(inventory => inventory.Quantity - (inventory.ReservedQuantity ?? 0m)));
    }

    public async Task<Dictionary<long, ProductLabelItem>> GetProductLabelsByIdsAsync(IReadOnlyCollection<long> productIds)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<long, ProductLabelItem>();
        }

        return await _context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new ProductLabelItem(p.Id, p.Name, p.Sku))
            .ToDictionaryAsync(p => p.Id);
    }

    public async Task<List<MyGarage.Models.Inventory>> GetBranchInventoriesForProductAsync(
        long branchId,
        long productId,
        bool onlyWithReserved = false)
    {
        var query = _context.Inventories
            .Where(i =>
                i.ProductId == productId &&
                i.Warehouse.BranchId == branchId &&
                i.Warehouse.IsDeleted != true &&
                i.Warehouse.IsActive != false);

        if (onlyWithReserved)
        {
            query = query
                .Where(i => (i.ReservedQuantity ?? 0m) > 0m)
                .OrderByDescending(i => i.ReservedQuantity ?? 0m);
        }
        else
        {
            query = query.OrderByDescending(i => i.Quantity - (i.ReservedQuantity ?? 0m));
        }

        return await query.ToListAsync();
    }

    public async Task<Customer?> GetCustomerByIdAsync(long customerId, bool asNoTracking = false)
    {
        IQueryable<Customer> query = _context.Customers;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(c => c.Id == customerId);
    }

    public async Task<bool> ActiveCustomerExistsAsync(long customerId)
    {
        return await _context.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Id == customerId && c.IsDeleted != true);
    }

    public async Task<Customer?> FindActiveCustomerByNormalizedPhoneAsync(string normalizedPhone)
    {
        return await _context.Customers.FirstOrDefaultAsync(c =>
            c.IsDeleted != true &&
            c.Phone != null &&
            c.Phone.Replace(" ", string.Empty) == normalizedPhone);
    }

    public async Task<List<Customer>> GetCustomersByIdsAsync(IReadOnlyCollection<long> customerIds)
    {
        if (customerIds.Count == 0)
        {
            return new List<Customer>();
        }

        return await _context.Customers
            .Where(x => customerIds.Contains(x.Id))
            .ToListAsync();
    }

    public async Task<Vehicle?> GetVehicleByIdNoTrackingAsync(long vehicleId)
    {
        return await _context.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vehicleId);
    }

    public async Task<Vehicle?> GetVehicleByPlateAsync(string plate)
    {
        return await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == plate);
    }

    public async Task<long?> GetVehicleModelIdAsync(long vehicleId, bool requireNotDeleted = false)
    {
        var query = _context.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == vehicleId);

        if (requireNotDeleted)
        {
            query = query.Where(v => v.IsDeleted != true);
        }

        return await query.Select(v => (long?)v.ModelId).FirstOrDefaultAsync();
    }

    public async Task<Dictionary<long, long>> GetActiveVehicleModelIdsByVehicleIdsAsync(IReadOnlyCollection<long> vehicleIds)
    {
        if (vehicleIds.Count == 0)
        {
            return new Dictionary<long, long>();
        }

        return await _context.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicleIds.Contains(vehicle.Id) && vehicle.IsDeleted != true)
            .ToDictionaryAsync(vehicle => vehicle.Id, vehicle => vehicle.ModelId);
    }

    public async Task<string?> GetVehicleModelFuelTypeAsync(long vehicleModelId)
    {
        return await _context.VehicleModels
            .AsNoTracking()
            .Where(model => model.Id == vehicleModelId && model.IsDeleted != true)
            .Select(model => model.FuelType)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CustomerOwnsVehicleAsync(long customerId, long vehicleId)
    {
        return await _context.CustomerVehicles
            .AsNoTracking()
            .AnyAsync(cv => cv.CustomerId == customerId && cv.VehicleId == vehicleId && cv.IsDeleted != true);
    }

    public async Task<CustomerVehicle?> GetCustomerVehicleLinkAsync(long customerId, long vehicleId)
    {
        return await _context.CustomerVehicles.FirstOrDefaultAsync(cv =>
            cv.CustomerId == customerId &&
            cv.VehicleId == vehicleId &&
            cv.IsDeleted != true);
    }

    public async Task<bool> ActiveInsuranceCompanyExistsAsync(long insuranceCompanyId)
    {
        return await _context.InsuranceCompanies
            .AsNoTracking()
            .AnyAsync(company => company.Id == insuranceCompanyId && company.IsDeleted != true);
    }

    public async Task<Dictionary<long, string>> GetActiveInsuranceCompanyNamesByIdsAsync(IReadOnlyCollection<long> insuranceCompanyIds)
    {
        if (insuranceCompanyIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        return await _context.InsuranceCompanies
            .AsNoTracking()
            .Where(company => insuranceCompanyIds.Contains(company.Id) && company.IsDeleted != true)
            .ToDictionaryAsync(company => company.Id, company => company.Name ?? $"#{company.Id}");
    }

    public async Task<ServiceOrder?> GetServiceOrderByAppointmentIdNoTrackingAsync(long appointmentId)
    {
        return await _context.ServiceOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(order => order.AppointmentId == appointmentId);
    }

    public async Task<List<AuditLog>> GetAppointmentEditRequestLogsAsync(long appointmentId, int take = 50)
    {
        return await _context.AuditLogs
            .AsNoTracking()
            .Where(log =>
                log.Action == "REQUEST_EDIT" &&
                log.TableName == "APPOINTMENTS" &&
                log.RecordId == appointmentId)
            .OrderByDescending(log => log.Id)
            .Take(take)
            .ToListAsync();
    }

    public async Task<bool> HasAppointmentInsuranceAssetsAsync(long appointmentId)
    {
        return await _context.AppointmentInsuranceAssets
            .AsNoTracking()
            .AnyAsync(asset => asset.AppointmentId == appointmentId);
    }

    public async Task<List<AppointmentInsuranceAsset>> GetAppointmentInsuranceAssetsAsync(long appointmentId)
    {
        return await _context.AppointmentInsuranceAssets
            .AsNoTracking()
            .Where(asset => asset.AppointmentId == appointmentId)
            .OrderBy(asset => asset.SortOrder)
            .ThenBy(asset => asset.Id)
            .ToListAsync();
    }

    public async Task<List<AppointmentInsuranceAsset>> GetAppointmentInsuranceAssetsByAppointmentIdsAsync(IReadOnlyCollection<long> appointmentIds)
    {
        if (appointmentIds.Count == 0)
        {
            return new List<AppointmentInsuranceAsset>();
        }

        return await _context.AppointmentInsuranceAssets
            .AsNoTracking()
            .Where(asset => appointmentIds.Contains(asset.AppointmentId))
            .OrderBy(asset => asset.SortOrder)
            .ThenBy(asset => asset.Id)
            .ToListAsync();
    }

    public async Task<List<AppointmentFlaggedWorkSlotSnapshot>> GetFlaggedWorkSlotsByAppointmentIdsAsync(IReadOnlyCollection<long> appointmentIds)
    {
        if (appointmentIds.Count == 0)
        {
            return new List<AppointmentFlaggedWorkSlotSnapshot>();
        }

        return await _context.WorkSlots
            .AsNoTracking()
            .Where(slot =>
                slot.AppointmentId.HasValue &&
                appointmentIds.Contains(slot.AppointmentId.Value) &&
                slot.Status != nameof(WorkSlotStatus.CANCELED) &&
                slot.Status != nameof(WorkSlotStatus.NOSHOW) &&
                slot.Status != nameof(WorkSlotStatus.COMPLETED) &&
                slot.Notes != null &&
                (slot.Notes.Contains(WorkSlotNoteMarkers.FlexPendingBayAssignment) ||
                 slot.Notes.Contains(WorkSlotNoteMarkers.PartsShortageAlert)))
            .Select(slot => new AppointmentFlaggedWorkSlotSnapshot(slot.AppointmentId!.Value, slot.Notes))
            .ToListAsync();
    }

    public async Task<bool> HasServiceOrderInsuranceAssetsAsync(long serviceOrderId)
    {
        return await _context.ServiceOrderInsuranceAssets
            .AsNoTracking()
            .AnyAsync(asset => asset.ServiceOrderId == serviceOrderId);
    }

    public async Task<List<AppointmentInsuranceUpload>> GetStagedInsuranceUploadsByUrlsAsync(IReadOnlyCollection<string> urls)
    {
        if (urls.Count == 0)
        {
            return new List<AppointmentInsuranceUpload>();
        }

        return await _context.AppointmentInsuranceUploads
            .Where(upload =>
                upload.Status == AppointmentInsuranceUploadStatuses.Staged &&
                upload.AppointmentId == null &&
                urls.Contains(upload.FileUrl))
            .ToListAsync();
    }

    public void AddCustomer(Customer customer) => _context.Customers.Add(customer);
    public void AddVehicle(Vehicle vehicle) => _context.Vehicles.Add(vehicle);
    public void AddCustomerVehicle(CustomerVehicle link) => _context.CustomerVehicles.Add(link);
    public void AddAppointmentInsuranceAssets(IEnumerable<AppointmentInsuranceAsset> assets) => _context.AppointmentInsuranceAssets.AddRange(assets);
    public void AddServiceOrderInsuranceAssets(IEnumerable<ServiceOrderInsuranceAsset> assets) => _context.ServiceOrderInsuranceAssets.AddRange(assets);
    public void AddServiceOrderDetails(IEnumerable<ServiceOrderDetail> details) => _context.ServiceOrderDetails.AddRange(details);
}
