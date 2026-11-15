using MyGarage.Services;
using MyGarage.Data;
using MyGarage.DTOs.Requests.Auth;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MyGarage.Tests;

public sealed class GoogleIdentityLinkTests
{
    [Theory]
    [InlineData(null, "subject-1")]
    [InlineData("", "subject-1")]
    [InlineData("subject-1", "subject-1")]
    public void ExistingIdentityOrUnlinkedAccountCanProceed(string? existing, string incoming)
        => AuthService.EnsureGoogleIdentityMatches(existing, incoming);

    [Fact]
    public void DifferentGoogleIdentityCannotReplaceExistingLink()
        => Assert.Throws<UnauthorizedAccessException>(() => AuthService.EnsureGoogleIdentityMatches("subject-1", "subject-2"));

    [Fact]
    public async Task NewGoogleCustomer_RejectsTakenPhoneBeforeCreatingAccount()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db => { TestWebApplicationFactory.SeedBasicBranchAndUsers(db); return Task.CompletedTask; });
        using var scope = factory.Services.CreateScope();
        var service = Assert.IsType<AuthService>(scope.ServiceProvider.GetRequiredService<MyGarage.Interfaces.IAuthService>());
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var payload = new MyGarage.Interfaces.GoogleTokenPayload(Email: "new@example.com", EmailVerified: true, Subject: "google-new", Name: "Customer");
        await Assert.ThrowsAsync<MyGarage.Services.AuthRequestException>(() => service.GoogleLoginValidatedAsync(payload, new GoogleLoginRequestDto { Phone = "0901112233" }));
        Assert.False(await db.Customers.AnyAsync(x => x.GoogleId == "google-new"));
        var result = await service.GoogleLoginValidatedAsync(payload, new GoogleLoginRequestDto { Phone = "0907778888" });
        Assert.NotNull(result.Token);
        Assert.Equal("INDIVIDUAL", (await db.Customers.SingleAsync(x => x.GoogleId == "google-new")).Type);
    }
}
