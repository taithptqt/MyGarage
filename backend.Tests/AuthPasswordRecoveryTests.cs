using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyGarage.Data;
using MyGarage.DTOs.Requests.Auth;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class AuthPasswordRecoveryTests
{
    [Fact]
    public async Task ForgotPasswordAsync_UnknownEmail_IsNeutral_AndDoesNotPersistToken()
    {
        await using var context = CreateContext(nameof(ForgotPasswordAsync_UnknownEmail_IsNeutral_AndDoesNotPersistToken));
        var emailService = new FakeEmailService();
        var rateLimitHook = new FakeAuthRateLimitHook();
        var sut = CreateAuthService(context, emailService, rateLimitHook);

        await sut.ForgotPasswordAsync(new ForgotPasswordRequestDto
        {
            Email = "missing@example.com"
        });

        Assert.Empty(await context.PasswordResetTokens.ToListAsync());
        Assert.Empty(emailService.Messages);
        Assert.Equal(["missing@example.com"], rateLimitHook.ForgotPasswordKeys);
    }

    [Fact]
    public async Task ForgotPasswordAsync_KnownCustomer_PersistsHashedToken_AndSendsResetLink()
    {
        await using var context = CreateContext(nameof(ForgotPasswordAsync_KnownCustomer_PersistsHashedToken_AndSendsResetLink));
        context.Customers.Add(new Customer
        {
            Id = 101,
            FullName = "Customer One",
            Email = "customer@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("old-password"),
            SecurityStamp = "stamp-customer",
            Type = "CUSTOMER",
            IsActive = true,
            IsDeleted = false
        });
        await context.SaveChangesAsync();

        var emailService = new FakeEmailService();
        var sut = CreateAuthService(context, emailService);

        await sut.ForgotPasswordAsync(new ForgotPasswordRequestDto
        {
            Email = "customer@example.com"
        });

        var storedToken = await context.PasswordResetTokens.SingleAsync();
        Assert.Equal(101, storedToken.CustomerId);
        Assert.Null(storedToken.StaffId);
        Assert.Null(storedToken.UsedAt);
        Assert.Null(storedToken.RevokedAt);
        Assert.True(storedToken.ExpiresAt > storedToken.CreatedAt);

        var message = Assert.Single(emailService.Messages);
        Assert.Equal("customer@example.com", message.ToEmail);
        Assert.Contains("reset-password?token=", message.Body, StringComparison.OrdinalIgnoreCase);

        var rawToken = ExtractQueryParameter(message.Body, "token");
        Assert.False(string.IsNullOrWhiteSpace(rawToken));
        Assert.NotEqual(rawToken, storedToken.TokenHash);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenCustomerAndStaffShareEmail_TargetsCustomerAudience()
    {
        await using var context = CreateContext(nameof(ForgotPasswordAsync_WhenCustomerAndStaffShareEmail_TargetsCustomerAudience));
        context.Customers.Add(new Customer
        {
            Id = 101,
            FullName = "Customer First",
            Email = "shared@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("customer-pass"),
            Type = "CUSTOMER",
            IsActive = true,
            IsDeleted = false
        });
        context.Staffs.Add(new Staff
        {
            Id = 201,
            FullName = "Staff Second",
            Email = "shared@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("staff-pass"),
            Status = "ACTIVE",
            IsDeleted = false
        });
        await context.SaveChangesAsync();

        var emailService = new FakeEmailService();
        var sut = CreateAuthService(context, emailService);

        await sut.ForgotPasswordAsync(new ForgotPasswordRequestDto
        {
            Email = "shared@example.com"
        });

        var storedToken = await context.PasswordResetTokens.SingleAsync();
        Assert.Equal(101, storedToken.CustomerId);
        Assert.Null(storedToken.StaffId);
    }

    [Fact]
    public async Task ForgotPasswordAsync_NewRequest_RevokesOutstandingUnusedTokens()
    {
        await using var context = CreateContext(nameof(ForgotPasswordAsync_NewRequest_RevokesOutstandingUnusedTokens));
        context.Customers.Add(new Customer
        {
            Id = 101,
            FullName = "Customer One",
            Email = "customer@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("old-password"),
            Type = "CUSTOMER",
            IsActive = true,
            IsDeleted = false
        });
        context.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = 9001,
            CustomerId = 101,
            TokenHash = "existing-hash",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });
        await context.SaveChangesAsync();

        var emailService = new FakeEmailService();
        var sut = CreateAuthService(context, emailService);

        await sut.ForgotPasswordAsync(new ForgotPasswordRequestDto
        {
            Email = "customer@example.com"
        });

        var tokens = await context.PasswordResetTokens.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.NotNull(tokens[0].RevokedAt);
        Assert.Null(tokens[1].RevokedAt);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_UpdatesPassword_MarksTokenUsed_AndRotatesSecurityStamp()
    {
        await using var context = CreateContext(nameof(ResetPasswordAsync_ValidToken_UpdatesPassword_MarksTokenUsed_AndRotatesSecurityStamp));
        context.Customers.Add(new Customer
        {
            Id = 101,
            FullName = "Customer One",
            Email = "customer@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("old-password"),
            SecurityStamp = "stamp-old",
            AccessFailedCount = 3,
            IsLockedOut = true,
            LockoutEnd = DateTime.UtcNow.AddHours(1),
            Type = "CUSTOMER",
            IsActive = true,
            IsDeleted = false
        });
        await context.PasswordResetTokens.AddAsync(new PasswordResetToken
        {
            Id = 9001,
            CustomerId = 101,
            TokenHash = AuthService.ComputePasswordResetTokenHashForTests("valid-reset-token"),
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });
        await context.PasswordResetTokens.AddAsync(new PasswordResetToken
        {
            Id = 9002,
            CustomerId = 101,
            TokenHash = "another-token-hash",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });
        await context.SaveChangesAsync();

        var sut = CreateAuthService(context);

        await sut.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Token = "valid-reset-token",
            NewPassword = "new-password-123"
        });

        var customer = await context.Customers.SingleAsync(x => x.Id == 101);
        Assert.True(BCrypt.Net.BCrypt.Verify("new-password-123", customer.PasswordHash));
        Assert.NotEqual("stamp-old", customer.SecurityStamp);
        Assert.Equal(0, customer.AccessFailedCount);
        Assert.False(customer.IsLockedOut);
        Assert.Null(customer.LockoutEnd);

        var tokens = await context.PasswordResetTokens.OrderBy(x => x.Id).ToListAsync();
        Assert.NotNull(tokens[0].UsedAt);
        Assert.Null(tokens[0].RevokedAt);
        Assert.NotNull(tokens[1].RevokedAt);
    }

    [Fact]
    public async Task ResetPasswordAsync_UsedOrExpiredToken_ThrowsUnauthorizedAccessException()
    {
        await using var context = CreateContext(nameof(ResetPasswordAsync_UsedOrExpiredToken_ThrowsUnauthorizedAccessException));
        context.Customers.Add(new Customer
        {
            Id = 101,
            FullName = "Customer One",
            Email = "customer@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("old-password"),
            Type = "CUSTOMER",
            IsActive = true,
            IsDeleted = false
        });
        context.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = 9001,
            CustomerId = 101,
            TokenHash = AuthService.ComputePasswordResetTokenHashForTests("expired-token"),
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        await context.SaveChangesAsync();

        var sut = CreateAuthService(context);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Token = "expired-token",
            NewPassword = "new-password-123"
        }));
    }

    [Fact]
    public async Task AuthSecurityStampValidator_RejectsPrincipalWithStaleStamp()
    {
        await using var context = CreateContext(nameof(AuthSecurityStampValidator_RejectsPrincipalWithStaleStamp));
        context.Customers.Add(new Customer
        {
            Id = 101,
            FullName = "Customer One",
            Email = "customer@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("old-password"),
            SecurityStamp = "current-stamp",
            Type = "CUSTOMER",
            IsActive = true,
            IsDeleted = false
        });
        await context.SaveChangesAsync();

        var sut = new AuthSecurityStampValidator(context);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "101"),
            new Claim(ClaimTypes.Role, "Customer"),
            new Claim("security_stamp", "stale-stamp")
        ], "Test"));

        var isValid = await sut.ValidateAsync(principal);

        Assert.False(isValid);
    }

    private static AuthService CreateAuthService(
        GarageContext context,
        FakeEmailService? emailService = null,
        FakeAuthRateLimitHook? rateLimitHook = null)
    {
        return new AuthService(
            context,
            BuildConfiguration(),
            emailService ?? new FakeEmailService(),
            new FakeClock(DateTime.UtcNow),
            new AuthCredentialRecoveryValidator(),
            rateLimitHook ?? new FakeAuthRateLimitHook());
    }

    private static GarageContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new GarageContext(options, new HttpContextAccessor());
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SuperSecretKeyForMyGarageBackendThatIsLongEnoughToWork",
                ["Jwt:Issuer"] = "MyGarageServer",
                ["Jwt:Audience"] = "MyGarageClients",
                ["Jwt:ExpireDays"] = "7",
                ["Auth:PasswordResetTokenExpiryMinutes"] = "15",
                ["Auth:PasswordResetUrlBase"] = "https://frontend.example.com/reset-password"
            })
            .Build();
    }

    private static string ExtractQueryParameter(string html, string key)
    {
        var marker = $"{key}=";
        var start = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        Assert.True(start >= 0, $"Missing query parameter '{key}' in email body.");

        start += marker.Length;
        var end = html.IndexOfAny(['"', '\'', '&', '<'], start);
        if (end < 0)
        {
            end = html.Length;
        }

        return html[start..end];
    }

    private sealed class FakeEmailService : IEmailService
    {
        public List<(string ToEmail, string Subject, string Body)> Messages { get; } = [];

        public Task SendEmailAsync(string toEmail, string subject, string body)
        {
            Messages.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock : ISystemClock
    {
        private readonly DateTime _utcNow;

        public FakeClock(DateTime utcNow)
        {
            _utcNow = utcNow;
        }

        public DateTime UtcNow => _utcNow;

        public DateTime LocalNow => _utcNow;

        public DateOnly LocalToday => DateOnly.FromDateTime(_utcNow);
    }

    private sealed class FakeAuthRateLimitHook : IAuthRateLimitHook
    {
        public List<string> ForgotPasswordKeys { get; } = [];
        public List<string> ResetPasswordKeys { get; } = [];
        public bool AllowForgotPassword { get; set; } = true;
        public bool AllowResetPassword { get; set; } = true;

        public Task<bool> CanRequestPasswordResetAsync(string normalizedEmail)
        {
            ForgotPasswordKeys.Add(normalizedEmail);
            return Task.FromResult(AllowForgotPassword);
        }

        public Task<bool> CanSubmitPasswordResetAsync(string tokenFingerprint)
        {
            ResetPasswordKeys.Add(tokenFingerprint);
            return Task.FromResult(AllowResetPassword);
        }
    }
}
