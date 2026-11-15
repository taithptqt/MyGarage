using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.DTOs.Responses.Auth;
using MyGarage.Interfaces;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class AuthFailureContractTests
{
    [Theory]
    [InlineData("sessions")]
    [InlineData("accounts")]
    [InlineData("external-sessions/google")]
    public async Task UnexpectedFailure_Returns500WithoutExceptionDetails(string route)
    {
        await using var factory = new TestWebApplicationFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.AddSingleton<IAuthService>(new ThrowingAuthService(new InvalidOperationException("secret database details", new Exception("secret inner details"))));
        }));
        using var client = app.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/" + route,
            new { identifier = "customer@example.com", password = "password", fullName = "Customer", phone = "0901112233", idToken = "test" });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("accounts", HttpStatusCode.Conflict)]
    [InlineData("external-sessions/google", HttpStatusCode.BadRequest)]
    public async Task ExpectedAccountError_PreservesClientContract(string route, HttpStatusCode expected)
    {
        await using var factory = new TestWebApplicationFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.AddSingleton<IAuthService>(new ThrowingAuthService(new AuthRequestException("account conflict")));
        }));
        using var client = app.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/" + route,
            new { password = "password", fullName = "Customer", phone = "0901112233", idToken = "test" });
        Assert.Equal(expected, response.StatusCode);
        Assert.Contains("account conflict", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("sessions")]
    [InlineData("external-sessions/google")]
    public async Task InvalidCredentials_ReturnsUnauthorized(string route)
    {
        await using var factory = new TestWebApplicationFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.AddSingleton<IAuthService>(new ThrowingAuthService(new UnauthorizedAccessException("invalid credentials")));
        }));
        using var client = app.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/" + route,
            new { identifier = "customer@example.com", password = "password", idToken = "test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private sealed class ThrowingAuthService(Exception exception) : IAuthService
    {
        public Task<AuthResponseDto> LoginAsync(LoginRequestDto request) => throw exception;
        public Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request) => throw exception;
        public Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginRequestDto request) => throw exception;
        public Task ForgotPasswordAsync(ForgotPasswordRequestDto request) => throw exception;
        public Task ResetPasswordAsync(ResetPasswordRequestDto request) => throw exception;
        public Task ChangePasswordAsync(long userId, string role, ChangePasswordRequestDto request) => throw exception;
        public Task<UserProfileResponseDto> GetProfileAsync(long userId, string role) => throw exception;
    }
}
