using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using MyGarage.Interfaces;
using MyGarage.Application.Payments;

namespace MyGarage.Controllers.OData;

internal static class ODataQueryLimits
{
    public const int PageSize = 100;
    public const int MaxTop = 100;
    public const int MaxSkip = 10_000;
    public const int MaxNodeCount = 40;
    public const int MaxOrderByNodeCount = 3;

    public const AllowedQueryOptions AllowedOptions =
        AllowedQueryOptions.Select |
        AllowedQueryOptions.Filter |
        AllowedQueryOptions.OrderBy |
        AllowedQueryOptions.Skip |
        AllowedQueryOptions.Top |
        AllowedQueryOptions.Count;

    public const AllowedFunctions AllowedFilterFunctions =
        AllowedFunctions.Contains |
        AllowedFunctions.StartsWith |
        AllowedFunctions.EndsWith |
        AllowedFunctions.ToLower |
        AllowedFunctions.ToUpper |
        AllowedFunctions.Trim;

    public const AllowedLogicalOperators AllowedFilterOperators =
        AllowedLogicalOperators.Equal |
        AllowedLogicalOperators.NotEqual |
        AllowedLogicalOperators.And |
        AllowedLogicalOperators.Or |
        AllowedLogicalOperators.GreaterThan |
        AllowedLogicalOperators.GreaterThanOrEqual |
        AllowedLogicalOperators.LessThan |
        AllowedLogicalOperators.LessThanOrEqual;
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class SafeEnableQueryAttribute : EnableQueryAttribute
{
    public SafeEnableQueryAttribute()
    {
        PageSize = ODataQueryLimits.PageSize;
        MaxTop = ODataQueryLimits.MaxTop;
        MaxSkip = ODataQueryLimits.MaxSkip;
        MaxExpansionDepth = 0;
        MaxNodeCount = ODataQueryLimits.MaxNodeCount;
        MaxOrderByNodeCount = ODataQueryLimits.MaxOrderByNodeCount;
        AllowedQueryOptions = ODataQueryLimits.AllowedOptions;
        AllowedFunctions = ODataQueryLimits.AllowedFilterFunctions;
        AllowedArithmeticOperators = AllowedArithmeticOperators.None;
        AllowedLogicalOperators = ODataQueryLimits.AllowedFilterOperators;
    }
}

[Authorize]
[Route("odata/ServiceOrders")]
[EnableRateLimiting("odata")]
public sealed class ServiceOrdersODataController(IODataQueryService queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get() => Ok(queries.ServiceOrders(User));
}

[Authorize]
[Route("odata/Quotations")]
[EnableRateLimiting("odata")]
public sealed class QuotationsODataController(IODataQueryService queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get() => Ok(queries.Quotations(User));
}

[Authorize]
[Route("odata/AuditLogs")]
[EnableRateLimiting("odata")]
public sealed class AuditLogsODataController(IODataQueryService queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get()
    {
        if (!User.IsAdminStaff() && !User.IsStaffWithAnyKeyword("MANAGER"))
        {
            return Forbid();
        }

        return Ok(queries.AuditLogs(User));
    }
}

[Authorize]
[Route("odata/Vehicles")]
[EnableRateLimiting("odata")]
public sealed class VehiclesODataController(IODataQueryService queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get() => Ok(queries.Vehicles(User));
}

[Authorize]
[Route("odata/Appointments")]
[EnableRateLimiting("odata")]
public sealed class AppointmentsODataController(IODataQueryService queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get() => Ok(queries.Appointments(User));
}

[Authorize]
[Route("odata/Inventory")]
[EnableRateLimiting("odata")]
public sealed class InventoryODataController(IODataQueryService queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get()
    {
        if (!User.IsStaffUser())
        {
            return Forbid();
        }

        return Ok(queries.Inventory(User));
    }
}

[Authorize]
[Route("odata/Invoices")]
[EnableRateLimiting("odata")]
public sealed class InvoicesODataController(InvoiceQuery query) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get()
    {
        var scope = User.IsStaffUser()
            ? (User.IsAdminStaff() ? new InvoiceScope(InvoiceScopeKind.All, null) : new InvoiceScope(InvoiceScopeKind.Branch, User.GetCurrentBranchId()))
            : new InvoiceScope(InvoiceScopeKind.Customer, User.GetCurrentUserId());
        return Ok(query.Execute(scope));
    }
}

[Authorize]
[Route("odata/Staffs")]
[EnableRateLimiting("odata")]
public sealed class StaffsODataController(IODataQueryService queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get()
    {
        if (!User.IsStaffUser())
        {
            return Forbid();
        }

        return Ok(queries.Staffs(User));
    }
}

[Authorize]
[Route("odata/Customers")]
[EnableRateLimiting("odata")]
public sealed class CustomersODataController(IODataQueryService queries) : ODataController
{
    [HttpGet]
    [SafeEnableQuery]
    public IActionResult Get()
    {
        if (!User.IsStaffUser())
        {
            return Forbid();
        }

        return Ok(queries.Customers(User));
    }
}
