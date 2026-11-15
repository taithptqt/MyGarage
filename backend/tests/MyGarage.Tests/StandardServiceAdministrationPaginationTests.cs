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

public class StandardServiceAdministrationPaginationTests
{
    [Fact]
    public async Task ServiceList_FiltersBeforePagingAndPreservesCatalogueAndSummary()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.ServiceCategories.Add(new ServiceCategory { Id = 1, Code = "CAT", Name = "Maintenance" });
            for (var i = 1; i <= 23; i++)
                db.StandardServices.Add(new StandardService { Id = i, Code = $"S{i:D2}", Name = $"Service {i}",
                    CategoryId = 1, BasePrice = i * 100, IsActive = i == 23 ? null : true });
            db.StandardServices.AddRange(
                new StandardService { Id = 24, Code = "  pkg-hidden", Name = "Package", CategoryId = 1, IsActive = true },
                new StandardService { Id = 25, Code = "INACTIVE", Name = "Inactive", CategoryId = 1, IsActive = false },
                new StandardService { Id = 26, Code = "DELETED", Name = "Deleted", CategoryId = 1, IsDeleted = true, IsActive = true });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/v1/standard-services/admin-list?search=maintenance&page=3&pageSize=10&branchId=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(23, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(23, json.RootElement.GetProperty("totalServices").GetInt32());
        Assert.Equal(22, json.RootElement.GetProperty("activeServices").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal("Maintenance", json.RootElement.GetProperty("categoryNames").GetProperty("1").GetString());
        Assert.Equal(21, json.RootElement.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(2100, json.RootElement.GetProperty("items")[0].GetProperty("basePrice").GetDecimal());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
            var legacy = mapper.Map<StandardServiceResponseDto>(db.StandardServices.Single(x => x.Id == 21));
            var expected = JsonSerializer.SerializeToNode(legacy, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var actual = JsonNode.Parse(json.RootElement.GetProperty("items")[0].GetRawText());
            Assert.True(JsonNode.DeepEquals(expected, actual));
        }

        var versioned = await client.GetAsync("/api/v1/standard-services/admin-list?search=maintenance&page=3&pageSize=10&branchId=1");
        Assert.Equal(HttpStatusCode.OK, versioned.StatusCode);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(await response.Content.ReadAsStringAsync()), JsonNode.Parse(await versioned.Content.ReadAsStringAsync())));

        var nameOnly = await client.GetAsync("/api/v1/standard-services/admin-list?search=maintenance&nameCodeOnly=true");
        using var nameOnlyJson = JsonDocument.Parse(await nameOnly.Content.ReadAsStringAsync());
        Assert.Equal(0, nameOnlyJson.RootElement.GetProperty("total").GetInt32());

        var inactive = await client.GetAsync("/api/v1/standard-services/admin-list?search=inactive");
        using var inactiveJson = JsonDocument.Parse(await inactive.Content.ReadAsStringAsync());
        Assert.Equal(1, inactiveJson.RootElement.GetProperty("total").GetInt32());
        var empty = await client.GetAsync("/api/v1/standard-services/admin-list?page=2147483647&pageSize=100");
        using var emptyJson = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());
        Assert.Empty(emptyJson.RootElement.GetProperty("items").EnumerateArray());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/standard-services/admin-list")).StatusCode);
    }
}
