using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfServiceOrderDataService(
    IGarageContext context,
    IAuditLogWriter auditLogService) : IServiceOrderDataService
{
    public async Task<(IReadOnlyList<ServiceOrder> Items, int Total)> GetPagedAsync(
        long? scopedBranchId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.ServiceOrders.AsNoTracking().AsQueryable();

        if (scopedBranchId.HasValue)
        {
            query = query.Where(x => x.ExecuteBranchId == scopedBranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status.Trim());
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var paged = offset >= total
            ? new List<ServiceOrder>()
            : await query.OrderBy(x => x.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return (paged, total);
    }

    public async Task<IReadOnlyList<AuditLog>> GetEditRequestLogsAsync(
        long serviceOrderId,
        CancellationToken ct = default) =>
        await context.AuditLogs
            .AsNoTracking()
            .Where(log =>
                log.Action == "REQUEST_EDIT" &&
                log.TableName == "SERVICEORDERS" &&
                log.RecordId == serviceOrderId)
            .OrderByDescending(log => log.Id)
            .Take(100)
            .ToListAsync(ct);

    public async Task DecideEditRequestAsync(
        AuditLog requestLog,
        AuditLog decisionLog,
        CancellationToken ct = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await TryBeginTransactionAsync(ct);
            try
            {
                await auditLogService.UpdateAsync(requestLog.Id, requestLog);
                await auditLogService.AddAsync(decisionLog);

                if (transaction != null)
                {
                    await transaction.CommitAsync(ct);
                }
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(ct);
                }

                throw;
            }
        });
    }

    public async Task AddInsuranceUploadInTransactionAsync(
        ServiceOrderInsuranceUpload upload,
        CancellationToken ct = default)
    {
        context.ServiceOrderInsuranceUploads.Add(upload);
        await SaveChangesInTransactionAsync(ct);
    }

    public async Task<ServiceOrderMappingBatchData> GetBatchMappingDataAsync(
        IReadOnlyCollection<long> orderIds,
        IReadOnlyCollection<long> vehicleIds,
        CancellationToken ct = default)
    {
        var assetLookup = await context.ServiceOrderInsuranceAssets
            .AsNoTracking()
            .Where(asset => orderIds.Contains(asset.ServiceOrderId))
            .OrderBy(asset => asset.ServiceOrderId)
            .ThenBy(asset => asset.AssetType)
            .ThenBy(asset => asset.SortOrder)
            .ThenBy(asset => asset.Id)
            .ToListAsync(ct);

        var assetsByOrderId = assetLookup
            .GroupBy(asset => asset.ServiceOrderId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var vehicleById = await context.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicleIds.Contains(vehicle.Id))
            .Select(vehicle => new ServiceOrderVehicleSnapshot
            {
                VehicleId = vehicle.Id,
                LicensePlate = vehicle.LicensePlate,
                ModelId = vehicle.ModelId,
                ModelName = vehicle.Model != null ? vehicle.Model.ModelName : null,
                BrandId = vehicle.Model != null ? (long?)vehicle.Model.BrandId : null,
                BrandName = vehicle.Model != null && vehicle.Model.Brand != null
                    ? vehicle.Model.Brand.Name
                    : null
            })
            .ToDictionaryAsync(item => item.VehicleId, ct);

        var insuranceCompanyByOrderId = await context.ServiceOrderDetails
            .AsNoTracking()
            .Where(detail =>
                orderIds.Contains(detail.ServiceOrderId) &&
                detail.InsuranceCompanyId.HasValue &&
                detail.InsuranceCompanyId.Value > 0)
            .GroupBy(detail => detail.ServiceOrderId)
            .Select(group => new
            {
                ServiceOrderId = group.Key,
                InsuranceCompanyId = group
                    .OrderBy(detail => detail.Id)
                    .Select(detail => detail.InsuranceCompanyId)
                    .FirstOrDefault()
            })
            .ToDictionaryAsync(item => item.ServiceOrderId, item => item.InsuranceCompanyId, ct);

        var discountByOrderId = await context.ServiceOrderDiscounts
            .AsNoTracking()
            .Where(discount => orderIds.Contains(discount.ServiceOrderId))
            .GroupBy(discount => discount.ServiceOrderId)
            .Select(group => new
            {
                ServiceOrderId = group.Key,
                TotalDiscount = group.Sum(discount =>
                    discount.AppliedAmount > 0m ? discount.AppliedAmount : (discount.DiscountValue < 0m ? 0m : discount.DiscountValue))
            })
            .ToDictionaryAsync(item => item.ServiceOrderId, item => item.TotalDiscount, ct);

        return new ServiceOrderMappingBatchData(
            assetsByOrderId,
            vehicleById,
            insuranceCompanyByOrderId,
            discountByOrderId);
    }

    public async Task<ServiceOrderMappingSingleData> GetSingleMappingDataAsync(
        long serviceOrderId,
        long vehicleId,
        CancellationToken ct = default)
    {
        var assets = await context.ServiceOrderInsuranceAssets
            .AsNoTracking()
            .Where(asset => asset.ServiceOrderId == serviceOrderId)
            .OrderBy(asset => asset.AssetType)
            .ThenBy(asset => asset.SortOrder)
            .ThenBy(asset => asset.Id)
            .ToListAsync(ct);

        var insuranceCompanyId = await context.ServiceOrderDetails
            .AsNoTracking()
            .Where(detail =>
                detail.ServiceOrderId == serviceOrderId &&
                detail.InsuranceCompanyId.HasValue &&
                detail.InsuranceCompanyId.Value > 0)
            .OrderBy(detail => detail.Id)
            .Select(detail => detail.InsuranceCompanyId)
            .FirstOrDefaultAsync(ct);

        var totalDiscount = await context.ServiceOrderDiscounts
            .AsNoTracking()
            .Where(discount => discount.ServiceOrderId == serviceOrderId)
            .SumAsync(
                discount => (decimal?)(discount.AppliedAmount > 0m ? discount.AppliedAmount : (discount.DiscountValue < 0m ? 0m : discount.DiscountValue)),
                ct) ?? 0m;

        var vehicle = await context.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == vehicleId)
            .Select(v => new ServiceOrderVehicleSnapshot
            {
                VehicleId = v.Id,
                LicensePlate = v.LicensePlate,
                ModelId = v.ModelId,
                ModelName = v.Model != null ? v.Model.ModelName : null,
                BrandId = v.Model != null ? (long?)v.Model.BrandId : null,
                BrandName = v.Model != null && v.Model.Brand != null
                    ? v.Model.Brand.Name
                    : null
            })
            .FirstOrDefaultAsync(ct);

        return new ServiceOrderMappingSingleData(assets, vehicle, insuranceCompanyId, totalDiscount);
    }

    public async Task<(string RoleCode, string RoleName)> GetStaffRoleInfoAsync(
        long staffId,
        CancellationToken ct = default)
    {
        var roleInfo = await (
            from staff in context.Staffs.AsNoTracking()
            join role in context.Roles.AsNoTracking() on staff.RoleId equals role.Id into roleJoin
            from role in roleJoin.DefaultIfEmpty()
            where staff.Id == staffId
            select new
            {
                role.RoleCode,
                role.RoleName
            }).FirstOrDefaultAsync(ct);

        return (roleInfo?.RoleCode ?? string.Empty, roleInfo?.RoleName ?? string.Empty);
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken ct = default)
    {
        if (context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await context.Database.BeginTransactionAsync(ct);
    }

    private async Task SaveChangesInTransactionAsync(CancellationToken ct = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(ct);
            await context.SaveChangesAsync(ct);
            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        });
    }
}
