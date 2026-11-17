using System.Security.Claims;
using MyGarage.DTOs.OData;

namespace MyGarage.Interfaces;

public interface IServiceOrderODataQuery { IQueryable<ServiceOrderODataDto> ServiceOrders(ClaimsPrincipal user); }
public interface IQuotationODataQuery { IQueryable<QuotationODataDto> Quotations(ClaimsPrincipal user); }
public interface IAuditLogODataQuery { IQueryable<AuditLogODataDto> AuditLogs(ClaimsPrincipal user); }
public interface IVehicleODataQuery { IQueryable<VehicleODataDto> Vehicles(ClaimsPrincipal user, bool includeDeleted = false); }
public interface IAppointmentODataQuery { IQueryable<AppointmentODataDto> Appointments(ClaimsPrincipal user); }
public interface IInventoryODataQuery { IQueryable<InventoryODataDto> Inventory(ClaimsPrincipal user); }
public interface IStaffODataQuery { IQueryable<StaffODataDto> Staffs(ClaimsPrincipal user, bool includeDeleted = false); }
public interface ICustomerODataQuery { IQueryable<CustomerODataDto> Customers(ClaimsPrincipal user, bool includeDeleted = false); }

// Preserve the existing aggregate contract for consumers that require several resources.
public interface IODataQueryService : IServiceOrderODataQuery, IQuotationODataQuery,
    IAuditLogODataQuery, IVehicleODataQuery, IAppointmentODataQuery, IInventoryODataQuery,
    IStaffODataQuery, ICustomerODataQuery
{
}