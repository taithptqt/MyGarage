using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MyGarage.Controllers;
using MyGarage.DTOs.OData;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class ODataQueryService : IODataQueryService
{
    private readonly IGarageContext _context;

    public ODataQueryService(IGarageContext context)
    {
        _context = context;
    }

    public IQueryable<ServiceOrderODataDto> ServiceOrders(ClaimsPrincipal user)
    {
        var query = _context.ServiceOrders.AsNoTracking();
        if (user.IsStaffUser())
        {
            if (!user.IsAdminStaff())
            {
                var branchId = user.GetCurrentBranchId();
                if (!branchId.HasValue)
                {
                    return Enumerable.Empty<ServiceOrderODataDto>().AsQueryable();
                }

                query = query.Where(x => x.ExecuteBranchId == branchId.Value);
            }
        }
        else
        {
            if (!string.Equals(user.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
                return Enumerable.Empty<ServiceOrderODataDto>().AsQueryable();
            var customerId = user.GetCurrentUserId();
            if (!customerId.HasValue)
            {
                return Enumerable.Empty<ServiceOrderODataDto>().AsQueryable();
            }

            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        return query.Select(x => new ServiceOrderODataDto
        {
            Id = x.Id,
            Code = x.Code,
            CustomerId = x.CustomerId,
            CustomerName = x.Customer.FullName,
            VehicleId = x.VehicleId,
            LicensePlate = x.Vehicle.LicensePlate,
            AppointmentId = x.AppointmentId,
            ExecuteBranchId = x.ExecuteBranchId,
            BranchName = x.ExecuteBranch.Name,
            OrderCategory = x.OrderCategory,
            Status = x.Status,
            PaymentStatus = x.PaymentStatus,
            SubTotalAmount = x.SubTotalAmount,
            TaxAmount = x.TaxAmount,
            FinalAmount = x.FinalAmount,
            CheckInTime = x.CheckInTime,
            EstimatedHandover = x.EstimatedHandover,
            CreatedAt = x.CreatedAt
        });
    }

    public IQueryable<QuotationODataDto> Quotations(ClaimsPrincipal user)
    {
        var query = _context.Quotations.AsNoTracking();
        if (user.IsStaffUser())
        {
            if (!user.IsAdminStaff())
            {
                var branchId = user.GetCurrentBranchId();
                if (!branchId.HasValue)
                {
                    return Enumerable.Empty<QuotationODataDto>().AsQueryable();
                }

                query = query.Where(x => x.ServiceOrder.ExecuteBranchId == branchId.Value);
            }
        }
        else
        {
            if (!string.Equals(user.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
                return Enumerable.Empty<QuotationODataDto>().AsQueryable();
            var customerId = user.GetCurrentUserId();
            if (!customerId.HasValue)
            {
                return Enumerable.Empty<QuotationODataDto>().AsQueryable();
            }

            query = query.Where(x => x.ServiceOrder.CustomerId == customerId.Value);
        }

        return query.Select(x => new QuotationODataDto
        {
            Id = x.Id,
            Code = x.Code,
            ServiceOrderId = x.ServiceOrderId,
            ServiceOrderCode = x.ServiceOrder.Code,
            ExecuteBranchId = x.ServiceOrder.ExecuteBranchId,
            CustomerId = x.ServiceOrder.CustomerId,
            CustomerName = x.ServiceOrder.Customer.FullName,
            QuotationType = x.QuotationType,
            VersionNo = x.VersionNo,
            Status = x.Status,
            TechnicalApprovalStatus = x.TechnicalApprovalStatus,
            FinancialApprovalStatus = x.FinancialApprovalStatus,
            CustomerDecisionStatus = x.CustomerDecisionStatus,
            SubTotalAmount = x.SubTotalAmount,
            TaxAmount = x.TaxAmount,
            FinalAmount = x.FinalAmount,
            TotalInsuranceAmount = x.TotalInsuranceAmount,
            TotalCustomerAmount = x.TotalCustomerAmount,
            ValidUntil = x.ValidUntil,
            CreatedAt = x.CreatedAt
        });
    }

    public IQueryable<AuditLogODataDto> AuditLogs(ClaimsPrincipal user)
    {
        if (!user.IsStaffUser())
        {
            return Enumerable.Empty<AuditLogODataDto>().AsQueryable();
        }

        var query = _context.AuditLogs.AsNoTracking();
        if (!user.IsAdminStaff())
        {
            var branchId = user.GetCurrentBranchId();
            if (!branchId.HasValue)
            {
                return Enumerable.Empty<AuditLogODataDto>().AsQueryable();
            }

            query = query.Where(x => x.BranchId == branchId.Value);
        }

        return query.Select(x => new AuditLogODataDto
        {
            Id = x.Id,
            UserId = x.UserId,
            UserType = x.UserType,
            BranchId = x.BranchId,
            Action = x.Action,
            TableName = x.TableName,
            RecordId = x.RecordId,
            // Historical audit values may contain secrets. Do not expose them through
            // OData until existing records can be reliably sanitized.
            OldValue = null,
            NewValue = null,
            Timestamp = x.Timestamp
        });
    }

    public IQueryable<VehicleODataDto> Vehicles(ClaimsPrincipal user, bool includeDeleted = false)
    {
        var query = _context.Vehicles.AsNoTracking();
        if (!includeDeleted || !user.IsAdminStaff())
            query = query.Where(v => v.IsDeleted != true);
        if (user.IsStaffUser())
        {
            if (!user.IsAdminStaff())
            {
                var branchId = user.GetCurrentBranchId();
                if (!branchId.HasValue)
                {
                    return Enumerable.Empty<VehicleODataDto>().AsQueryable();
                }

                query = query.Where(v =>
                    v.ServiceOrders.Any(so => so.ExecuteBranchId == branchId.Value) ||
                    v.Appointments.Any(a => a.BranchId == branchId.Value));
            }
        }
        else
        {
            if (!string.Equals(user.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
                return Enumerable.Empty<VehicleODataDto>().AsQueryable();
            var customerId = user.GetCurrentUserId();
            if (!customerId.HasValue)
            {
                return Enumerable.Empty<VehicleODataDto>().AsQueryable();
            }

            query = query.Where(v => v.CustomerVehicles.Any(cv =>
                cv.CustomerId == customerId.Value && cv.IsDeleted != true && cv.IsCurrentOwner != false));
        }

        return query.Select(v => new VehicleODataDto
        {
            Id = v.Id,
            LicensePlate = v.LicensePlate,
            Vin = v.Vin,
            ModelId = v.ModelId,
            ModelName = v.Model.ModelName,
            Color = v.Color,
            Odo = v.Odo,
            ManufactureYear = v.ManufactureYear,
            IsDeleted = v.IsDeleted,
            CreatedAt = v.CreatedAt
        });
    }

    public IQueryable<AppointmentODataDto> Appointments(ClaimsPrincipal user)
    {
        var query = _context.Appointments.AsNoTracking();
        if (user.IsStaffUser())
        {
            if (!user.IsAdminStaff())
            {
                var branchId = user.GetCurrentBranchId();
                if (!branchId.HasValue)
                {
                    return Enumerable.Empty<AppointmentODataDto>().AsQueryable();
                }

                query = query.Where(x => x.BranchId == branchId.Value);
            }
        }
        else
        {
            if (!string.Equals(user.GetCurrentRole(), "Customer", StringComparison.OrdinalIgnoreCase))
                return Enumerable.Empty<AppointmentODataDto>().AsQueryable();
            var customerId = user.GetCurrentUserId();
            if (!customerId.HasValue)
            {
                return Enumerable.Empty<AppointmentODataDto>().AsQueryable();
            }

            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        return query.Select(x => new AppointmentODataDto
        {
            Id = x.Id,
            BranchId = x.BranchId,
            BranchName = x.Branch.Name,
            CustomerId = x.CustomerId,
            CustomerName = x.Customer.FullName,
            VehicleId = x.VehicleId,
            LicensePlate = x.Vehicle != null ? x.Vehicle.LicensePlate : null,
            StartTime = x.StartTime,
            EndTime = x.EndTime,
            Status = x.Status,
            CustomerNotes = x.CustomerNotes,
            CreatedAt = x.CreatedAt
        });
    }

    public IQueryable<InventoryODataDto> Inventory(ClaimsPrincipal user)
    {
        if (!user.IsStaffUser())
        {
            return Enumerable.Empty<InventoryODataDto>().AsQueryable();
        }

        var query = _context.Inventories.AsNoTracking();
        if (!user.IsAdminStaff())
        {
            var branchId = user.GetCurrentBranchId();
            if (!branchId.HasValue)
            {
                return Enumerable.Empty<InventoryODataDto>().AsQueryable();
            }

            query = query.Where(x => x.Warehouse.BranchId == branchId.Value);
        }

        return query.Select(x => new InventoryODataDto
        {
            Id = x.Id,
            WarehouseId = x.WarehouseId,
            WarehouseName = x.Warehouse.Name,
            BranchId = x.Warehouse.BranchId,
            ProductId = x.ProductId,
            ProductSku = x.Product.Sku,
            ProductName = x.Product.Name,
            Quantity = x.Quantity,
            ReservedQuantity = x.ReservedQuantity,
            MinStockLevel = x.MinStockLevel,
            MaxStockLevel = x.MaxStockLevel,
            CurrentMacPrice = x.CurrentMacPrice,
            TotalInventoryValue = x.TotalInventoryValue,
            LocationBin = x.LocationBin
        });
    }

    public IQueryable<StaffODataDto> Staffs(ClaimsPrincipal user, bool includeDeleted = false)
    {
        if (!user.IsStaffUser())
        {
            return Enumerable.Empty<StaffODataDto>().AsQueryable();
        }

        var query = _context.Staffs.AsNoTracking();
        if (!includeDeleted || !user.IsAdminStaff())
            query = query.Where(x => x.IsDeleted != true);
        if (!user.IsAdminStaff())
        {
            var branchId = user.GetCurrentBranchId();
            if (!branchId.HasValue)
            {
                return Enumerable.Empty<StaffODataDto>().AsQueryable();
            }

            query = query.Where(x => x.BranchId == branchId.Value);
        }

        return query.Select(x => new StaffODataDto
        {
            Id = x.Id,
            FullName = x.FullName,
            Phone = x.Phone,
            Email = x.Email,
            BranchId = x.BranchId,
            BranchName = x.Branch != null ? x.Branch.Name : null,
            RoleId = x.RoleId,
            RoleCode = x.Role != null ? x.Role.RoleCode : null,
            RoleName = x.Role != null ? x.Role.RoleName : null,
            Status = x.Status,
            IsDeleted = x.IsDeleted,
            CreatedAt = x.CreatedAt
        });
    }

    public IQueryable<CustomerODataDto> Customers(ClaimsPrincipal user, bool includeDeleted = false)
    {
        if (!user.IsStaffUser())
        {
            return Enumerable.Empty<CustomerODataDto>().AsQueryable();
        }

        var query = _context.Customers.AsNoTracking();
        if (!includeDeleted || !user.IsAdminStaff())
            query = query.Where(c => c.IsDeleted != true);
        if (!user.IsAdminStaff())
        {
            var branchId = user.GetCurrentBranchId();
            if (!branchId.HasValue)
            {
                return Enumerable.Empty<CustomerODataDto>().AsQueryable();
            }

            query = query.Where(c =>
                c.ServiceOrders.Any(so => so.ExecuteBranchId == branchId.Value) ||
                c.Appointments.Any(a => a.BranchId == branchId.Value) ||
                c.RetailOrders.Any(ro => ro.BranchId == branchId.Value));
        }

        return query.Select(c => new CustomerODataDto
        {
            Id = c.Id,
            FullName = c.FullName,
            Phone = c.Phone,
            Email = c.Email,
            Type = c.Type,
            TierId = c.TierId,
            TotalSpending = c.TotalSpending,
            IsActive = c.IsActive,
            IsDeleted = c.IsDeleted,
            CreatedAt = c.CreatedAt
        });
    }
}
