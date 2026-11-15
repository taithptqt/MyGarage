using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class ServiceCategoryPaginationTests
{
    [Fact]
    public async Task DualRoutes_PreserveLegacyDtoAndRejectAnonymous()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceCategories.Add(new ServiceCategory { Id = 8, Code = "MAINT", Name = "Bảo dưỡng",
                Description = "Mô tả", ParentCategoryId = 7, IsActive = false, IsDeleted = true,
                CreatedAt = new DateTime(2026, 1, 1), CreatedBy = 200, UpdatedBy = 201, DisplayOrder = 2 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/service-categories/admin-list")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var versioned = await client.GetAsync("/api/v1/service-categories/admin-list?search=%20B%E1%BA%A2O%20&page=0&pageSize=0");
        Assert.Equal(HttpStatusCode.OK, versioned.StatusCode);
        var versionedJson = JsonNode.Parse(await versioned.Content.ReadAsStringAsync())!;
        Assert.Equal(1, (int)versionedJson["total"]!);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
        var expected = JsonSerializer.SerializeToNode(mapper.Map<ServiceCategoryResponseDto>(db.ServiceCategories.Single()), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(JsonNode.DeepEquals(expected, versionedJson["items"]![0]));
    }

    [Fact]
    public async Task SearchIsAppliedBeforePagingAndExistingVisibilityIsPreserved()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var i = 1; i <= 25; i++)
                db.ServiceCategories.Add(new ServiceCategory { Id = i, Code = $"CAT{i:D2}",
                    Name = i <= 23 ? $"Engine {i}" : $"Body {i}", IsActive = i != 22, IsDeleted = i == 23 });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/v1/service-categories/admin-list?search=ENGINE&page=3&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(23, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(21, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.True(json.RootElement.GetProperty("items")[2].GetProperty("isDeleted").GetBoolean());

        var empty = await client.GetAsync("/api/v1/service-categories/admin-list?page=2147483647&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        using var emptyJson = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());
        Assert.Empty(emptyJson.RootElement.GetProperty("items").EnumerateArray());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/service-categories/admin-list")).StatusCode);
    }
}
