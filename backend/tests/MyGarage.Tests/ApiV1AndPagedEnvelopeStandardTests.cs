using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class ApiV1AndPagedEnvelopeStandardTests
{
    [Fact]
    public void AllRestControllers_ShouldExposeApiV1Route()
    {
        var assembly = typeof(Program).Assembly;
        var restControllers = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .Where(t => !typeof(ODataController).IsAssignableFrom(t))
            .ToList();

        Assert.True(restControllers.Count >= 120, $"Expected at least 120 REST controllers, but found {restControllers.Count}.");

        var missingApiV1Controllers = new List<string>();

        foreach (var controller in restControllers)
        {
            var classRoutes = controller.GetCustomAttributes<RouteAttribute>(inherit: true)
                .Select(r => r.Template ?? string.Empty)
                .ToList();

            var methodRoutes = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: true))
                .Select(a => a.Template ?? string.Empty)
                .ToList();

            var allTemplates = classRoutes.Concat(methodRoutes).ToList();
            var hasApiV1 = allTemplates.Any(t => t.Contains("api/v1", StringComparison.OrdinalIgnoreCase));

            if (!hasApiV1)
            {
                missingApiV1Controllers.Add(controller.FullName ?? controller.Name);
            }
        }

        Assert.True(
            missingApiV1Controllers.Count == 0,
            $"The following REST controllers do not expose an 'api/v1' route: {string.Join(", ", missingApiV1Controllers)}");
    }

    [Fact]
    public void PagedEnvelope_Create_ShouldComputeMetadataAccurately()
    {
        // Middle page
        var middle = PagedEnvelope<string>.Create(new[] { "a", "b" }, totalCount: 25, page: 2, pageSize: 10);
        Assert.Equal(2, middle.Items.Count);
        Assert.Equal(25, middle.TotalCount);
        Assert.Equal(25, middle.Total);
        Assert.Equal(2, middle.Page);
        Assert.Equal(10, middle.PageSize);
        Assert.Equal(3, middle.TotalPages);
        Assert.True(middle.HasPreviousPage);
        Assert.True(middle.HasNextPage);

        // Empty / totalCount: 0
        var empty = PagedEnvelope<string>.Create(Array.Empty<string>(), totalCount: 0, page: 1, pageSize: 10);
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, empty.Total);
        Assert.Equal(1, empty.Page);
        Assert.Equal(10, empty.PageSize);
        Assert.Equal(0, empty.TotalPages);
        Assert.False(empty.HasPreviousPage);
        Assert.False(empty.HasNextPage);

        // First page
        var first = PagedEnvelope<string>.Create(new[] { "a" }, totalCount: 25, page: 1, pageSize: 10);
        Assert.Equal(1, first.Page);
        Assert.Equal(3, first.TotalPages);
        Assert.False(first.HasPreviousPage);
        Assert.True(first.HasNextPage);

        // Last page
        var last = PagedEnvelope<string>.Create(new[] { "z" }, totalCount: 25, page: 3, pageSize: 10);
        Assert.Equal(3, last.Page);
        Assert.Equal(3, last.TotalPages);
        Assert.True(last.HasPreviousPage);
        Assert.False(last.HasNextPage);
    }

    [Fact]
    public void PaginationExtensions_CreatePagedEnvelope_ShouldPopulateEnvelopeAndHeaders()
    {
        var httpContext = new DefaultHttpContext();
        var envelope = PaginationExtensions.CreatePagedEnvelope(
            new[] { 1, 2, 3 },
            totalCount: 15,
            page: 1,
            pageSize: 5,
            response: httpContext.Response);

        Assert.Equal(new[] { 1, 2, 3 }, envelope.Items);
        Assert.Equal(15, envelope.TotalCount);
        Assert.Equal(15, envelope.Total);
        Assert.Equal(1, envelope.Page);
        Assert.Equal(5, envelope.PageSize);
        Assert.Equal(3, envelope.TotalPages);
        Assert.False(envelope.HasPreviousPage);
        Assert.True(envelope.HasNextPage);

        Assert.False(httpContext.Response.Headers.ContainsKey(PaginationExtensions.HeaderTotalCount));
        Assert.False(httpContext.Response.Headers.ContainsKey(PaginationExtensions.HeaderPage));
        Assert.False(httpContext.Response.Headers.ContainsKey(PaginationExtensions.HeaderPageSize));
        Assert.False(httpContext.Response.Headers.ContainsKey(PaginationExtensions.HeaderTotalPages));
    }

    [Fact]
    public async Task AdminListAndPageEndpoints_OnApiV1_ShouldReturnUnifiedPagedEnvelopeFields()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);

            if (!db.VehicleBrands.Any(b => b.Id == 1))
            {
                db.VehicleBrands.Add(new VehicleBrand
                {
                    Id = 1,
                    Code = "TOYOTA",
                    Name = "Toyota",
                    CountryOfOrigin = "Japan",
                    IsActive = true
                });
            }

            if (!db.VehicleModels.Any(m => m.Id == 1))
            {
                db.VehicleModels.Add(new VehicleModel
                {
                    Id = 1,
                    BrandId = 1,
                    ModelName = "Camry",
                    Segment = "Sedan",
                    FuelType = "Gasoline",
                    IsDeleted = false
                });
            }

            if (!db.Products.Any(p => p.Id == 1))
            {
                db.Products.Add(new Product
                {
                    Id = 1,
                    Sku = "PT-001",
                    Name = "Lọc dầu",
                    Unit = "Cái",
                    CostPrice = 100000m,
                    RetailPrice = 150000m,
                    IsActive = true,
                    IsDeleted = false
                });
            }

            return Task.CompletedTask;
        });

        using var adminClient = factory.CreateClient();
        var adminToken = TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 1. BranchAdministrationController (/api/v1/branches/admin-list)
        await AssertPagedEndpointAndSummaryAsync(
            adminClient,
            "/api/v1/branches/admin-list?page=1&pageSize=10",
            expectedLegacyFields: new[] { "totalBranches", "activeBranches" });

        // 2. CustomerAdministrationController (/api/v1/customers/admin-list)
        await AssertPagedEndpointAndSummaryAsync(
            adminClient,
            "/api/v1/customers/admin-list?page=1&pageSize=10",
            expectedLegacyFields: new[] { "totalCustomers", "activeCustomers" });

        // 3. VehicleBrandAdministrationController (/api/v1/vehicle-brands/admin-list)
        await AssertPagedEndpointAndSummaryAsync(
            adminClient,
            "/api/v1/vehicle-brands/admin-list?page=1&pageSize=10",
            expectedLegacyFields: new[] { "totalBrands", "activeBrands", "inactiveBrands" });

        // 4. VehicleModelAdministrationController (/api/v1/vehicle-models/admin-list)
        await AssertPagedEndpointAndSummaryAsync(
            adminClient,
            "/api/v1/vehicle-models/admin-list?page=1&pageSize=10",
            expectedLegacyFields: new[] { "totalModels", "brandCount", "activeModels" });

        // 5. InventoryPartsPageController (/api/v1/inventory/parts-page)
        await AssertPagedEndpointAndSummaryAsync(
            adminClient,
            "/api/v1/inventory/parts-page?page=1&pageSize=10",
            expectedLegacyFields: Array.Empty<string>());

        // 6. Converted CRUD endpoints on /api/v1
        await AssertPagedEndpointAndSummaryAsync(
            adminClient,
            "/api/v1/products?page=1&pageSize=10",
            expectedLegacyFields: Array.Empty<string>());

        await AssertPagedEndpointAndSummaryAsync(
            adminClient,
            "/api/v1/vehicles?page=1&pageSize=10",
            expectedLegacyFields: Array.Empty<string>());

        await AssertPagedEndpointAndSummaryAsync(
            adminClient,
            "/api/v1/customers?page=1&pageSize=10",
            expectedLegacyFields: Array.Empty<string>());

        await AssertPagedEndpointAndSummaryAsync(
            adminClient,
            "/api/v1/audit-logs?page=1&pageSize=10",
            expectedLegacyFields: Array.Empty<string>());
    }

    private static async Task AssertPagedEndpointAndSummaryAsync(
        HttpClient client,
        string url,
        string[] expectedLegacyFields)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify X-* pagination headers are retired on REST endpoints
        Assert.False(response.Headers.Contains("X-Total-Count"), $"Unexpected header X-Total-Count on {url}");
        Assert.False(response.Headers.Contains("X-Page"), $"Unexpected header X-Page on {url}");
        Assert.False(response.Headers.Contains("X-Page-Size"), $"Unexpected header X-Page-Size on {url}");
        Assert.False(response.Headers.Contains("X-Total-Pages"), $"Unexpected header X-Total-Pages on {url}");

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Verify unified PagedEnvelope JSON fields
        Assert.True(root.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array, $"Missing or invalid 'items' in {url}");
        Assert.True(root.TryGetProperty("total", out var totalProp) && totalProp.ValueKind == JsonValueKind.Number, $"Missing 'total' in {url}");
        Assert.True(root.TryGetProperty("totalCount", out var totalCountProp) && totalCountProp.ValueKind == JsonValueKind.Number, $"Missing 'totalCount' in {url}");
        Assert.Equal(totalProp.GetInt32(), totalCountProp.GetInt32());

        Assert.True(root.TryGetProperty("page", out var pageProp) && pageProp.ValueKind == JsonValueKind.Number, $"Missing 'page' in {url}");
        Assert.Equal(1, pageProp.GetInt32());

        Assert.True(root.TryGetProperty("pageSize", out var pageSizeProp) && pageSizeProp.ValueKind == JsonValueKind.Number, $"Missing 'pageSize' in {url}");
        Assert.Equal(10, pageSizeProp.GetInt32());

        Assert.True(root.TryGetProperty("totalPages", out var totalPagesProp) && totalPagesProp.ValueKind == JsonValueKind.Number, $"Missing 'totalPages' in {url}");
        Assert.True(root.TryGetProperty("hasPreviousPage", out var hasPrevProp) && (hasPrevProp.ValueKind == JsonValueKind.True || hasPrevProp.ValueKind == JsonValueKind.False), $"Missing 'hasPreviousPage' in {url}");
        Assert.True(root.TryGetProperty("hasNextPage", out var hasNextProp) && (hasNextProp.ValueKind == JsonValueKind.True || hasNextProp.ValueKind == JsonValueKind.False), $"Missing 'hasNextPage' in {url}");

        // Verify legacy summary fields
        foreach (var field in expectedLegacyFields)
        {
            Assert.True(root.TryGetProperty(field, out var legacyProp) && legacyProp.ValueKind == JsonValueKind.Number, $"Missing legacy summary field '{field}' in {url}");
        }
    }
}
