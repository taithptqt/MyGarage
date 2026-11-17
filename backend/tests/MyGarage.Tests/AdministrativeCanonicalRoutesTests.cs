using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MyGarage.Tests;

public sealed class AdministrativeCanonicalRoutesTests
{
    [Fact]
    public void CanonicalAndLegacyRoutes_ResolveToSameActionWithoutAmbiguity()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => new
            {
                Path = e.RoutePattern.RawText?.TrimStart('/'),
                Methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods,
                Action = e.Metadata.GetMetadata<ControllerActionDescriptor>()
            }).Where(e => e.Action != null && e.Methods != null).ToList();

        var cases = new (string Method, string Canonical, string Legacy)[]
        {
            ("GET", "api/v1/staff", "api/v1/staffs"),
            ("POST", "api/v1/staff", "api/v1/staffs"),
            ("GET", "api/v1/staff/{id:long}", "api/v1/staffs/{id:long}"),
            ("PUT", "api/v1/staff/{id:long}", "api/v1/staffs/{id:long}"),
            ("DELETE", "api/v1/staff/{id:long}", "api/v1/staffs/{id:long}"),
            ("POST", "api/v1/staff/{id:long}/avatars", "api/v1/staffs/{id:long}/avatars"),
            ("GET", "api/v1/staff/admin-list", "api/v1/staffs/admin-list"),
            ("GET", "api/v1/admin/vehicles", "api/v1/vehicle-admin/list"),
            ("POST", "api/v1/admin/vehicles", "api/v1/vehicle-admin/vehicles"),
            ("GET", "api/v1/admin/vehicles/{id:long}", "api/v1/vehicle-admin/vehicles/{id:long}"),
            ("PUT", "api/v1/admin/vehicles/{id:long}", "api/v1/vehicle-admin/vehicles/{id:long}"),
            ("DELETE", "api/v1/admin/vehicles/{id:long}", "api/v1/vehicle-admin/vehicles/{id:long}"),
            ("PUT", "api/v1/admin/vehicles/{id:long}/edit-locks", "api/v1/vehicle-admin/vehicles/{id:long}/edit-locks"),
            ("GET", "api/v1/admin/bays", "api/v1/bay-admin/bays"),
            ("POST", "api/v1/admin/bays", "api/v1/bay-admin/bays"),
            ("GET", "api/v1/admin/bays/{id:long}", "api/v1/bay-admin/bays/{id:long}"),
            ("PUT", "api/v1/admin/bays/{id:long}", "api/v1/bay-admin/bays/{id:long}"),
            ("DELETE", "api/v1/admin/bays/{id:long}", "api/v1/bay-admin/bays/{id:long}"),
            ("POST", "api/v1/admin/bays/{id:long}/vehicle-assignments", "api/v1/bay-admin/bays/{id:long}/vehicle-assignments"),
            ("GET", "api/v1/admin/bays/summary", "api/v1/bay-admin/summary"),
            ("GET", "api/v1/salary-profiles", "api/v1/salary-admin/profiles"),
            ("POST", "api/v1/salary-profiles", "api/v1/salary-admin/profiles"),
            ("PUT", "api/v1/salary-profiles/{id:long}", "api/v1/salary-admin/profiles/{id:long}"),
            ("DELETE", "api/v1/salary-profiles/{id:long}", "api/v1/salary-admin/profiles/{id:long}"),
            ("GET", "api/v1/initial-salary-requests", "api/v1/salary-admin/requests"),
            ("POST", "api/v1/initial-salary-requests/{id:long}/approvals", "api/v1/salary-admin/requests/{id:long}/approvals"),
            ("POST", "api/v1/initial-salary-requests/{id:long}/rejections", "api/v1/salary-admin/requests/{id:long}/rejections"),
            ("GET", "api/v1/staff/current/payslips", "api/v1/payslips/my"),
            ("GET", "api/v1/chat/conversations", "api/v1/chat/my-conversations"),
            ("POST", "api/v1/service-orders/{serviceOrderId:long}/maintenance-reminder-schedules", "api/v1/maintenance-reminders/service-orders/{serviceOrderId:long}/schedules")
        };
        foreach (var (method, canonical, legacy) in cases)
        {
            var first = Assert.Single(endpoints.Where(e => e.Path == canonical && e.Methods!.Contains(method)));
            var second = Assert.Single(endpoints.Where(e => e.Path == legacy && e.Methods!.Contains(method)));
            Assert.Equal(second.Action!.ControllerTypeInfo, first.Action!.ControllerTypeInfo);
            Assert.Equal(second.Action.ActionName, first.Action.ActionName);
        }
    }
}