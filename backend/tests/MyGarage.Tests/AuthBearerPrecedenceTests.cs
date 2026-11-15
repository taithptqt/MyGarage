using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using MyGarage.Security;
using Xunit;

namespace MyGarage.Tests;

public sealed class AuthBearerPrecedenceTests
{
    [Fact]
    public async Task InvalidBearerDoesNotFallBackToValidCookie()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/profile");
        request.Headers.Add("Cookie", $"{AuthCookie.Name}={TestWebApplicationFactory.CreateJwtToken(100, "Customer")}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var write = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/auth/password");
        write.Headers.Add("Cookie", $"{AuthCookie.Name}={TestWebApplicationFactory.CreateJwtToken(100, "Customer")}");
        write.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        write.Content = System.Net.Http.Json.JsonContent.Create(new { oldPassword = "oldpass", newPassword = "newpass" });
        using var writeResponse = await client.SendAsync(write);
        Assert.Equal(HttpStatusCode.Unauthorized, writeResponse.StatusCode);

        using var validBearer = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/profile");
        validBearer.Headers.Add("Cookie", $"{AuthCookie.Name}=invalid-token");
        validBearer.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        using var validResponse = await client.SendAsync(validBearer);
        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
    }
}
