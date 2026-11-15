using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.Application.Ai;
using Xunit;

namespace MyGarage.Tests;

public sealed class AiModelConfigPageTests
{
    [Fact]
    public async Task Page_IsAdminOnly_DoesNotExposeKeys_AndBoundsPagination()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiModelConfigPageReader>();
            services.AddScoped<IAiModelConfigPageReader, FakeReader>();
        }));
        using var client = configured.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/ai-model-configs/page")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/ai-model-configs/page")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        var response = await client.GetAsync("/api/v1/ai-model-configs/page?page=0&pageSize=999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(100, json.RootElement.GetProperty("pageSize").GetInt32());
        var row = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.True(row.GetProperty("hasApiKey").GetBoolean());
        Assert.False(row.TryGetProperty("apiKey", out _));
    }

    private sealed class FakeReader : IAiModelConfigPageReader
    {
        public Task<(IReadOnlyList<AiModelConfigItem> Items, int Total)> ReadAsync(int page, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult<(IReadOnlyList<AiModelConfigItem>, int)>((new[] { new AiModelConfigItem(1, "provider", true, "model", 100, 0.5m, true) }, 1));
    }
}
