using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.Application.Ai;
using Xunit;

namespace MyGarage.Tests;

public sealed class AiKnowledgePageTests
{
    [Fact]
    public async Task Page_RequiresAdmin_AndBoundsPagination()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiKnowledgePageReader>();
            services.AddScoped<IAiKnowledgePageReader, FakeReader>();
        }));
        using var client = configured.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/ai-knowledge-bases/page")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/ai-knowledge-bases/page")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(201, "Staff", "ADMIN", 1));
        var response = await client.GetAsync("/api/v1/ai-knowledge-bases/page?page=0&pageSize=999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(100, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal("Bảo dưỡng", Assert.Single(json.RootElement.GetProperty("items").EnumerateArray()).GetProperty("topic").GetString());
    }

    private sealed class FakeReader : IAiKnowledgePageReader
    {
        public Task<(IReadOnlyList<AiKnowledgeItem> Items, int Total)> ReadAsync(int page, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult<(IReadOnlyList<AiKnowledgeItem>, int)>((new[] { new AiKnowledgeItem(1, "Bảo dưỡng", "Nội dung", null, false, null) }, 1));
    }
}
