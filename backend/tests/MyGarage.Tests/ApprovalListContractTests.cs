using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.Application.Approvals;
using Xunit;

namespace MyGarage.Tests;

public sealed class ApprovalListContractTests
{
    [Fact]
    public async Task List_RequiresStaffAndPreservesResponseAndHeaders()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IApprovalListReader>();
            services.AddScoped<IApprovalListReader, FakeReader>();
        }));
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/approval-requests")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/approval-requests")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        var response = await client.GetAsync("/api/approval-requests?page=2&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Total-Count"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());
        var row = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(2, row.GetProperty("id").GetInt64());
        Assert.True(row.TryGetProperty("serviceOrderDiscountId", out _));
        Assert.True(row.TryGetProperty("rejectReason", out _));
    }

    private sealed class FakeReader : IApprovalListReader
    {
        public Task<(IReadOnlyList<ApprovalListItem> Items, int Total)> ReadAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            IReadOnlyList<ApprovalListItem> items = [new ApprovalListItem(page, "GATE", null, null, null, 200, null, "PENDING", null, null, null, null)];
            return Task.FromResult((items, 2));
        }
    }
}
