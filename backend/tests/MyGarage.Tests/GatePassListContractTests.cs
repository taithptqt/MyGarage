using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.Application.GatePasses;
using Xunit;

namespace MyGarage.Tests;

public sealed class GatePassListContractTests
{
    [Fact]
    public async Task List_RequiresStaff_AndPreservesPagingAndResponseShape()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGatePassListReader>();
            services.AddScoped<IGatePassListReader, FakeReader>();
        }));
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/gate-passes")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/gate-passes")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/v1/gate-passes?page=2&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Total-Count"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("page").GetInt32());
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(2, item.GetProperty("id").GetInt64());
        Assert.Equal("QR", item.GetProperty("qrCode").GetString());
        Assert.True(item.TryGetProperty("oldPartsPackingListUrl", out _));
        Assert.True(item.TryGetProperty("securityCheckedAt", out _));
    }

    private sealed class FakeReader : IGatePassListReader
    {
        public Task<(IReadOnlyList<GatePassListItem> Items, int Total)> ReadAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            IReadOnlyList<GatePassListItem> items = [new GatePassListItem(page, 10, "NORMAL", "QR", 200, null,
                null, null, false, "ISSUED", null, null, null)];
            return Task.FromResult((items, 2));
        }
    }
}
