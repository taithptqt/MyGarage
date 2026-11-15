using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed partial class EfServiceOrderStore : IServiceOrderStore
{
    private const int MaxCodeGenerationAttempts = 8;
    private readonly IGarageContext _context;

    public EfServiceOrderStore(IGarageContext context)
    {
        _context = context;
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            await action();
            if (tx != null)
            {
                await tx.CommitAsync();
            }
        });
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, bool clearChangeTracker = false)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            if (clearChangeTracker)
            {
                _context.ChangeTracker.Clear();
            }

            await using var transaction = await TryBeginTransactionAsync();
            try
            {
                var result = await action();
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return result;
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }

                throw;
            }
        });
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }

    public async Task SaveChangesInTransactionAsync()
    {
        await ExecuteInTransactionAsync(async () =>
        {
            await _context.SaveChangesAsync();
        });
    }

    public async Task<ServiceOrder> CreateWithUniqueCodeAsync(
        ServiceOrder entity,
        Func<string> generateCode,
        Func<ServiceOrder, Task> persistAndAfterCreateAsync)
    {
        Exception? lastDuplicateException = null;
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var hasExternalTransaction = _context.Database.CurrentTransaction != null;

            for (var attempt = 0; attempt < MaxCodeGenerationAttempts; attempt++)
            {
                var candidateCode = generateCode();
                var exists = await _context.ServiceOrders
                    .AsNoTracking()
                    .AnyAsync(x => x.Code == candidateCode);
                if (exists)
                {
                    continue;
                }

                entity.Code = candidateCode;

                IDbContextTransaction? attemptTransaction = null;
                if (!hasExternalTransaction)
                {
                    attemptTransaction = await TryBeginTransactionAsync();
                }

                try
                {
                    await persistAndAfterCreateAsync(entity);
                    if (attemptTransaction != null)
                    {
                        await attemptTransaction.CommitAsync();
                    }

                    return entity;
                }
                catch (DbUpdateException ex) when (IsDuplicateCodeViolation(ex))
                {
                    lastDuplicateException = ex;
                    if (attemptTransaction != null)
                    {
                        await attemptTransaction.RollbackAsync();
                    }

                    DetachEntity(entity);
                }
                catch
                {
                    if (attemptTransaction != null)
                    {
                        await attemptTransaction.RollbackAsync();
                    }

                    throw;
                }
                finally
                {
                    if (attemptTransaction != null)
                    {
                        await attemptTransaction.DisposeAsync();
                    }
                }
            }

            throw new InvalidOperationException(
                "Không thể tạo mã lệnh sửa chữa duy nhất. Vui lòng thử lại.",
                lastDuplicateException);
        });
    }

    public Task<string?> GetServiceOrderStatusAsync(long serviceOrderId)
    {
        return _context.ServiceOrders
            .AsNoTracking()
            .Where(x => x.Id == serviceOrderId)
            .Select(x => x.Status)
            .FirstOrDefaultAsync();
    }

    public Task<ServiceOrder?> GetServiceOrderWithDiscountsAsync(long id)
    {
        return _context.ServiceOrders
            .Include(x => x.ServiceOrderDiscounts)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public Task<ServiceOrder?> GetServiceOrderWithDetailsAsNoTrackingAsync(long id)
    {
        return _context.ServiceOrders
            .AsNoTracking()
            .Include(o => o.ServiceOrderDetails)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public Task<ServiceOrder?> GetServiceOrderForUpdateAsync(long id)
    {
        return _context.ServiceOrders.FirstOrDefaultAsync(o => o.Id == id);
    }

    public Task<ServiceOrder?> GetServiceOrderAsNoTrackingAsync(long id)
    {
        return _context.ServiceOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public Task<List<ServiceOrderDetail>> GetEditableServiceDetailsAsync(long serviceOrderId)
    {
        return _context.ServiceOrderDetails
            .Where(d =>
                d.ServiceOrderId == serviceOrderId &&
                (d.ItemType == "LABOR" || d.ItemType == "SERVICE"))
            .OrderBy(d => d.Id)
            .ToListAsync();
    }

    public void RemoveServiceOrderDetail(ServiceOrderDetail detail)
    {
        _context.ServiceOrderDetails.Remove(detail);
    }

    public void AddServiceOrderDetail(ServiceOrderDetail detail)
    {
        _context.ServiceOrderDetails.Add(detail);
    }

    public void AddServiceOrderDetails(IEnumerable<ServiceOrderDetail> details)
    {
        _context.ServiceOrderDetails.AddRange(details);
    }

    public Task<bool> HasCheckInConditionLogAsync(long serviceOrderId, string logType)
    {
        return _context.VehicleConditionLogs
            .AsNoTracking()
            .AnyAsync(x => x.ServiceOrderId == serviceOrderId && x.LogType == logType);
    }

    public void AddVehicleConditionLog(VehicleConditionLog log)
    {
        _context.VehicleConditionLogs.Add(log);
    }

    public async Task<bool> IsBranchDayOffAsync(long branchId, int dayOfWeek)
    {
        var operatingHour = await _context.BranchOperatingHours
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DayOfWeek == dayOfWeek);
        return operatingHour?.IsDayOff == true;
    }

    public Task<Customer?> FindActiveCustomerByNormalizedPhoneAsync(string normalizedPhone)
    {
        return _context.Customers
            .FirstOrDefaultAsync(c =>
                c.IsDeleted != true &&
                c.Phone != null &&
                c.Phone.Replace(" ", string.Empty) == normalizedPhone);
    }

    public void AddCustomer(Customer customer)
    {
        _context.Customers.Add(customer);
    }

    public Task<Vehicle?> FindVehicleByLicensePlateAsync(string licensePlate)
    {
        return _context.Vehicles
            .FirstOrDefaultAsync(v => v.LicensePlate == licensePlate);
    }

    public void AddVehicle(Vehicle vehicle)
    {
        _context.Vehicles.Add(vehicle);
    }

    public Task<CustomerVehicle?> FindCustomerVehicleAsync(long customerId, long vehicleId)
    {
        return _context.CustomerVehicles
            .FirstOrDefaultAsync(cv => cv.CustomerId == customerId && cv.VehicleId == vehicleId);
    }

    public void AddCustomerVehicle(CustomerVehicle link)
    {
        _context.CustomerVehicles.Add(link);
    }

    public Task<bool> CustomerExistsAsync(long customerId)
    {
        return _context.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Id == customerId && c.IsDeleted != true);
    }

    public Task<bool> VehicleOwnedByCustomerAsync(long customerId, long vehicleId)
    {
        return _context.CustomerVehicles
            .AsNoTracking()
            .AnyAsync(cv =>
                cv.CustomerId == customerId &&
                cv.VehicleId == vehicleId &&
                cv.IsDeleted != true);
    }

    public Task<List<Package>> GetActivePackagesWithItemsAsync(IReadOnlyList<long> packageIds)
    {
        return _context.Packages
            .AsNoTracking()
            .Include(package => package.Category)
            .Include(package => package.PackageItems.Where(item => item.IsDeleted != true && item.IsActive != false))
                .ThenInclude(item => item.StandardService)
            .Where(package =>
                packageIds.Contains(package.Id) &&
                package.IsDeleted != true &&
                package.IsActive != false)
            .ToListAsync();
    }

    public Task<Dictionary<long, StandardService>> GetActiveStandardServicesMapAsync(IReadOnlyList<long> serviceIds)
    {
        return _context.StandardServices
            .AsNoTracking()
            .Where(service =>
                serviceIds.Contains(service.Id) &&
                service.IsDeleted != true &&
                service.IsActive != false)
            .ToDictionaryAsync(service => service.Id);
    }

    public Task<long?> GetAppointmentInsuranceCompanyIdAsync(long appointmentId)
    {
        return _context.Appointments
            .AsNoTracking()
            .Where(a => a.Id == appointmentId)
            .Select(a => a.InsuranceCompanyId)
            .FirstOrDefaultAsync();
    }

    public Task<bool> ActiveInsuranceCompanyExistsAsync(long insuranceCompanyId)
    {
        return _context.InsuranceCompanies
            .AsNoTracking()
            .AnyAsync(company => company.Id == insuranceCompanyId && company.IsDeleted != true);
    }

    public Task<StaffTierInfo?> GetStaffTierInfoAsync(long staffId)
    {
        return _context.Staffs
            .AsNoTracking()
            .Where(x => x.Id == staffId && x.IsDeleted != true)
            .Select(x => new StaffTierInfo(x.Id, x.StaffTierId))
            .FirstOrDefaultAsync();
    }

    public Task<bool> HasExistingAdvisorCommissionAsync(long serviceOrderId, long staffId, string sourceType, string logType, string canceledStatus)
    {
        return _context.CommissionTransactions
            .AsNoTracking()
            .AnyAsync(x =>
                x.ServiceOrderId == serviceOrderId &&
                x.StaffId == staffId &&
                x.SourceType == sourceType &&
                x.LogType == logType &&
                x.OriginalCommissionId == null &&
                x.Status != canceledStatus);
    }

    public Task<CommissionRule?> GetBestMatchingCommissionRuleAsync(long branchId, string targetType, long staffTierId, long staffId)
    {
        return _context.CommissionRules
            .AsNoTracking()
            .Where(x =>
                x.IsDeleted != true &&
                (x.IsActive ?? true) &&
                x.BranchId == branchId &&
                x.TargetType == targetType &&
                x.StaffTierId == staffTierId &&
                (x.TargetId == null || x.TargetId == staffId))
            .OrderByDescending(x => x.TargetId == staffId)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public void AddCommissionTransaction(CommissionTransaction tx)
    {
        _context.CommissionTransactions.Add(tx);
    }

    public Task<bool> HasCompletedPaymentRecordAsync(long serviceOrderId, string completedStatus)
    {
        return _context.Payments
            .AsNoTracking()
            .AnyAsync(x =>
                x.ServiceOrderId == serviceOrderId &&
                x.Amount > 0m &&
                x.Status != null &&
                x.Status.ToUpper() == completedStatus);
    }

    public Task<VehicleEngineFuelInfo?> GetVehicleEngineFuelInfoAsync(long vehicleId)
    {
        return _context.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.Id == vehicleId && vehicle.IsDeleted != true)
            .Select(vehicle => new VehicleEngineFuelInfo(vehicle.ModelId, vehicle.Model.FuelType))
            .FirstOrDefaultAsync();
    }

    public void AddInsuranceAssets(IEnumerable<ServiceOrderInsuranceAsset> rows)
    {
        _context.ServiceOrderInsuranceAssets.AddRange(rows);
    }

    public Task<List<ServiceOrderInsuranceAsset>> GetInsuranceAssetsByOrderIdAsync(long serviceOrderId)
    {
        return _context.ServiceOrderInsuranceAssets
            .Where(asset => asset.ServiceOrderId == serviceOrderId)
            .ToListAsync();
    }

    public void RemoveInsuranceAssets(IEnumerable<ServiceOrderInsuranceAsset> rows)
    {
        _context.ServiceOrderInsuranceAssets.RemoveRange(rows);
    }

    public Task<List<ServiceOrderInsuranceUpload>> GetStagedInsuranceUploadsAsync(IReadOnlyList<string> normalizedUrls, string stagedStatus)
    {
        return _context.ServiceOrderInsuranceUploads
            .Where(upload =>
                upload.Status == stagedStatus &&
                upload.ServiceOrderId == null &&
                normalizedUrls.Contains(upload.FileUrl))
            .ToListAsync();
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync();
    }

    private void DetachEntity(ServiceOrder entity)
    {
        var entry = _context.Entry(entity);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
    }

    private static bool IsDuplicateCodeViolation(Exception ex)
    {
        var cursor = ex;
        while (cursor != null)
        {
            if (cursor is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                return true;
            }

            if (cursor.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                cursor.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            cursor = cursor.InnerException;
        }

        return false;
    }
}
