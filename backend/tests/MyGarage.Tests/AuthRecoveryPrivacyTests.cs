using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.Interfaces;
using Xunit;

namespace MyGarage.Tests;

public sealed class AuthRecoveryPrivacyTests
{
    [Fact]
    public async Task ForgotPassword_EmailDeliveryFailureDoesNotRevealAccountExistence()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:PasswordResetUrlBase", "https://example.test/reset-password");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton<IEmailService, FailingEmailService>();
            });
        });
        using var client = app.CreateClient();
        using var existing = await client.PostAsJsonAsync("/api/v1/auth/credential-recovery-requests", new { email = "customer100@example.com" });
        using var missing = await client.PostAsJsonAsync("/api/v1/auth/credential-recovery-requests", new { email = "missing@example.com" });
        Assert.True(existing.StatusCode == HttpStatusCode.OK, await existing.Content.ReadAsStringAsync());
        Assert.Equal(existing.StatusCode, missing.StatusCode);
        Assert.Equal(await existing.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
        using var invalid = await client.PostAsJsonAsync("/api/v1/auth/credential-recovery-requests", new { email = "not-an-email" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private sealed class FailingEmailService : IEmailService
    {
        public Task SendEmailAsync(string toEmail, string subject, string body) => throw new InvalidOperationException("SMTP unavailable");
    }
}
