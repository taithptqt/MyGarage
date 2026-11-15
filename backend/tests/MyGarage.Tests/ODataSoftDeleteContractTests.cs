using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class ODataSoftDeleteContractTests
{
    [Theory]
    [InlineData("/odata")]
    [InlineData("/api/v1/odata")]
    public async Task DeletedRows_AreHiddenUnlessAdminRequestsThem(string prefix)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Customers.Add(new Customer { Id = 801, FullName = "Deleted customer", Phone = "0900000801", Type = "INDIVIDUAL", IsDeleted = true });
            db.Staffs.Add(new Staff { Id = 802, FullName = "Deleted staff", Phone = "0900000802", PasswordHash = "hash", BranchId = 1, IsDeleted = true });
            db.VehicleModels.Add(new VehicleModel { Id = 803, BrandId = 1, ModelName = "Model" });
            db.Vehicles.Add(new Vehicle { Id = 804, ModelId = 803, LicensePlate = "DELETED-804", IsDeleted = true });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        foreach (var (entity, id) in new[] { ("Customers", 801), ("Staffs", 802), ("Vehicles", 804) })
        {
            async Task<int> Count(string query)
            {
                using var response = await client.GetAsync($"{prefix}/{entity}?$filter=Id%20eq%20{id}&$count=true{query}");
                var payload = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == HttpStatusCode.OK, payload);
                using var json = JsonDocument.Parse(payload);
                return json.RootElement.GetProperty("value").GetArrayLength();
            }
            Assert.Equal(0, await Count(""));
            Assert.Equal(1, await Count("&includeDeleted=true"));
            using var invalid = await client.GetAsync($"{prefix}/{entity}?includeDeleted=invalid");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        foreach (var entity in new[] { "Customers", "Staffs", "Vehicles" })
        {
            using var response = await client.GetAsync($"{prefix}/{entity}?includeDeleted=true");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        using var customerResponse = await client.GetAsync($"{prefix}/Vehicles?includeDeleted=true");
        Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
    }
}
