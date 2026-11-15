using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class AiForecastListTests
{
    [Fact]
    public async Task List_PreservesLegacyAndSupportsServerPages()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            for (var id = 1; id <= 3; id++)
                db.AiForecasts.Add(new AiForecast { Id = id, VehicleId = 100 + id, Type = "SERVICE", PredictedDate = new DateOnly(2026, 1, id) });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/ai-forecasts")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var legacy = await client.GetAsync("/api/v1/ai-forecasts");
        Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
        using var all = JsonDocument.Parse(await legacy.Content.ReadAsStringAsync());
        Assert.Equal(3, all.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, all.RootElement.GetProperty("items").GetArrayLength());
        var response = await client.GetAsync("/api/v1/ai-forecasts?page=2&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Total-Count"));
        using var page = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, page.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(2, Assert.Single(page.RootElement.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
    }
}
