using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MyGarage.Tests;

public sealed class DiagnosisRouteCompatibilityTests
{
    [Fact]
    public async Task TechnicalApproval_RejectsImpersonatedReviewer()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "MANAGER", 1));
        using var response = await client.PostAsJsonAsync("/api/v1/service-orders/1/diagnosis-queues/technical-approvals", new { teamLeaderId = 202 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    public static IEnumerable<object[]> Routes()
    {
        yield return new object[] { "GET", "dispatch-suggestions" };
        yield return new object[] { "GET", "intake-suggestions" };
        yield return new object[] { "POST", "handover-snapshots" };
        yield return new object[] { "GET", "handover-snapshots/latest" };
        yield return new object[] { "POST", "handover-snapshots/{snapshotId:long}/customer-decisions" };
        yield return new object[] { "POST", "diagnosis-queues" };
        yield return new object[] { "PATCH", "diagnosis-queues" };
        yield return new object[] { "DELETE", "diagnosis-queues" };
        yield return new object[] { "POST", "diagnosis-queues/starts" };
        yield return new object[] { "POST", "diagnosis-queues/technical-approvals" };
        yield return new object[] { "POST", "diagnosis-queues/technical-rejections" };
        yield return new object[] { "POST", "diagnosis-queues/version-clones" };
        yield return new object[] { "POST", "diagnosis-queues/final-approvals" };
        yield return new object[] { "POST", "diagnosis-queues/foreman-decisions" };
        yield return new object[] { "POST", "diagnosis-queues/support-requests" };
        yield return new object[] { "POST", "diagnosis-queues/cancel-requests" };
        yield return new object[] { "POST", "diagnosis-queues/cancel-requests/decisions" };
        yield return new object[] { "POST", "diagnosis-queues/manager-overrides" };
        yield return new object[] { "POST", "diagnosis-queues/supporting-team-selections" };
    }

    [Fact]
    public async Task CanonicalAndLegacyRoutes_MapUniquelyToSameAction()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        foreach (var route in Routes())
        {
            var method = (string)route[0];
            var suffix = (string)route[1];
            var canonical = FindEndpoint(endpoints, $"api/v1/service-orders/{{serviceOrderId:long}}/{suffix}", method);
            var legacy = FindEndpoint(endpoints, $"api/v1/diagnoses/service-orders/{{serviceOrderId:long}}/{suffix}", method);
            var canonicalAction = canonical.Metadata.GetMetadata<ControllerActionDescriptor>();
            var legacyAction = legacy.Metadata.GetMetadata<ControllerActionDescriptor>();
            Assert.NotNull(canonicalAction);
            Assert.NotNull(legacyAction);
            Assert.Equal(legacyAction.MethodInfo, canonicalAction.MethodInfo);
            var actualSuffix = suffix.Replace("{snapshotId:long}", "17");
            foreach (var prefix in new[] { "/api/v1/service-orders/42/", "/api/v1/diagnoses/service-orders/42/" })
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), prefix + actualSuffix);
                using var response = await client.SendAsync(request);
                // Every route on DiagnosisController requires an authenticated Staff actor.
                Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }
    }

    private static RouteEndpoint FindEndpoint(IEnumerable<RouteEndpoint> endpoints, string path, string method)
    {
        return Assert.Single(endpoints.Where(endpoint =>
            string.Equals(endpoint.RoutePattern.RawText?.TrimStart('/'), path, StringComparison.OrdinalIgnoreCase) &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public async Task Swagger_ContainsBothRoutesWithoutConflicts()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        var document = factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        Assert.Contains("/api/v1/service-orders/{serviceOrderId}/diagnosis-queues", document.Paths.Keys);
        Assert.Contains("/api/v1/diagnoses/service-orders/{serviceOrderId}/diagnosis-queues", document.Paths.Keys);
    }
}
