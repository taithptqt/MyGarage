using System.Security.Claims;
using MyGarage.DTOs.OData;

namespace MyGarage.Interfaces;

public interface IODataQueryService
{
    IQueryable<ServiceOrderODataDto> ServiceOrders(ClaimsPrincipal user);
    IQueryable<QuotationODataDto> Quotations(ClaimsPrincipal user);
    IQueryable<AuditLogODataDto> AuditLogs(ClaimsPrincipal user);
    IQueryable<VehicleODataDto> Vehicles(ClaimsPrincipal user, bool includeDeleted = false);
    IQueryable<AppointmentODataDto> Appointments(ClaimsPrincipal user);
    IQueryable<InventoryODataDto> Inventory(ClaimsPrincipal user);
    IQueryable<StaffODataDto> Staffs(ClaimsPrincipal user, bool includeDeleted = false);
    IQueryable<CustomerODataDto> Customers(ClaimsPrincipal user, bool includeDeleted = false);
}
