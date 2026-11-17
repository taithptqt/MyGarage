using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MyGarage.Tests;

public sealed class AppointmentCanonicalRoutesTests
{
    [Theory]
    [InlineData("api/v1/customers/current/appointments", "api/v1/appointments/customers/current", 100L, "Customer", null)]
    [InlineData("api/v1/branches/{branchId:long}/appointments", "api/v1/appointments/branches/{branchId:long}", 200L, "Staff", "SERVICE_ADVISOR")]
    public async Task CanonicalAndLegacyRoutes_ResolveUniquelyAndReturnSamePage(
        string canonical, string legacy, long userId, string role, string? roleCode)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(userId, role, roleCode, role == "Staff" ? 1 : null));
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        RouteEndpoint Find(string path) => Assert.Single(endpoints, endpoint =>
            string.Equals(endpoint.RoutePattern.RawText?.TrimStart('/'), path, StringComparison.OrdinalIgnoreCase) &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true);
        Assert.Equal(Find(canonical).Metadata.GetMetadata<ControllerActionDescriptor>()!.MethodInfo,
            Find(legacy).Metadata.GetMetadata<ControllerActionDescriptor>()!.MethodInfo);
        var canonicalUrl = "/" + canonical.Replace("{branchId:long}", "1") + "?page=1&pageSize=10";
        var legacyUrl = "/" + legacy.Replace("{branchId:long}", "1") + "?page=1&pageSize=10";
        using var first = await client.GetAsync(canonicalUrl);
        using var second = await client.GetAsync(legacyUrl);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(first.StatusCode, second.StatusCode);
        using var a = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var b = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.True(JsonElement.DeepEquals(a.RootElement, b.RootElement));
    }
}