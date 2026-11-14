using System.Net;
using System.Net.Http.Headers;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class RateLimiterPipelineTests
{
    [Fact]
    public async Task ODataRateLimit_SeparatesAuthenticatedUsersSharingAddress()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Staffs.Add(new Staff { Id = 100, FullName = "Staff", Phone = "0901234567", Email = "staff100@example.com", PasswordHash = "hash", BranchId = 1, Status = "ACTIVE", SecurityStamp = "test-stamp" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));

        for (var i = 0; i < 60; i++)
        {
            using var response = await client.GetAsync("/odata/AiKnowledgeCatalog");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using var limited = await client.GetAsync("/odata/AiKnowledgeCatalog");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Staff", "MANAGER", 1));
        using var otherUser = await client.GetAsync("/odata/AiKnowledgeCatalog");
        Assert.Equal(HttpStatusCode.Forbidden, otherUser.StatusCode);
    }
}
