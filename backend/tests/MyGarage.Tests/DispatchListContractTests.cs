using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.Application.Ai;
using Xunit;

namespace MyGarage.Tests;

public sealed class DispatchListContractTests
{
    [Fact]
    public async Task List_RequiresStaff_AndPreservesPageAndFields()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDispatchListReader>();
            services.AddScoped<IDispatchListReader, FakeReader>();
        }));
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/ai-smart-dispatchings")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/ai-smart-dispatchings")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/v1/ai-smart-dispatchings?page=2&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Total-Count"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, body.RootElement.GetProperty("totalCount").GetInt32());
        var row = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(2, row.GetProperty("id").GetInt64());
        Assert.Equal(1, row.GetProperty("branchId").GetInt64());
        Assert.Equal("Advice", row.GetProperty("recommendation").GetString());
    }

    private sealed class FakeReader : IDispatchListReader
    {
        public Task<(IReadOnlyList<DispatchRow> Items, int Total)> ReadAsync(int? page, int? pageSize, CancellationToken cancellationToken)
        {
            IReadOnlyList<DispatchRow> rows = [new DispatchRow(page ?? 1, 1, new DateOnly(2026, 1, 1), "Advice", null, null, null)];
            return Task.FromResult((rows, 2));
        }
    }
}
