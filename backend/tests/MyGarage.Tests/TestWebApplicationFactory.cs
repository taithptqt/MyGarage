using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "MyGarageSuperSecretIntegrationTestingKey_32PlusBytes!";
    public const string TestJwtIssuer = "MyGarageServer";
    public const string TestJwtAudience = "MyGarageClients";

    private readonly string _environmentName;
    private readonly string _databaseName = $"MyGarageTests_{Guid.NewGuid():N}";

    public TestWebApplicationFactory() : this("Testing")
    {
    }

    public TestWebApplicationFactory(string environmentName)
    {
        _environmentName = environmentName;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.UseSetting("Jwt:Key", TestJwtKey);
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);
        builder.UseSetting("Frontend:BaseUrl", "https://localhost:5173");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GarageDbConnection"] = "Server=(localdb)\\mssqllocaldb;Database=MyGarageTestUnused;Trusted_Connection=True;",
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["Frontend:BaseUrl"] = "https://localhost:5173",
                ["AzureStorage:ConnectionString"] = "UseDevelopmentStorage=true",
                ["PayOS:ClientId"] = "test-client-id",
                ["PayOS:ApiKey"] = "test-api-key",
                ["PayOS:ChecksumKey"] = "test-checksum-key"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<GarageContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<GarageContext>>();
            services.RemoveAll<GarageContext>();
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IEmailService>();
            // GarageContext's InMemory model differs from the SQL read model; preserve seeded HTTP fixtures.
            services.RemoveAll<MyGarage.Application.Payments.ICustomerPaymentsReader>();
            services.AddScoped<MyGarage.Application.Payments.ICustomerPaymentsReader, MyGarage.Services.CustomerPaymentsReader>();
            services.RemoveAll<MyGarage.Application.Forecasts.IForecastListReader>();
            services.AddScoped<MyGarage.Application.Forecasts.IForecastListReader, MyGarage.Services.ForecastListReader>();
            services.RemoveAll<MyGarage.Application.Audit.IAdminAuditReader>();
            services.AddScoped<MyGarage.Application.Audit.IAdminAuditReader, MyGarage.Services.AdminAuditReader>();
            services.RemoveAll<MyGarage.Application.Ai.IDispatchBayCandidates>();
            services.AddScoped<MyGarage.Application.Ai.IDispatchBayCandidates, MyGarage.Services.DispatchBayCandidates>();
            services.RemoveAll<MyGarage.Application.Approvals.IApprovalListReader>();
            services.AddScoped<MyGarage.Application.Approvals.IApprovalListReader, MyGarage.Tests.ApprovalListFixtureReader>();

            services.AddSingleton<IEmailService, NoOpEmailService>();

            services.AddDbContext<GarageContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.ValidIssuer = TestJwtIssuer;
                options.TokenValidationParameters.ValidAudience = TestJwtAudience;
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
            });
        });
    }

    public async Task SeedAsync(Func<GarageContext, Task> seeder)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        await db.Database.EnsureCreatedAsync();
        await seeder(db);
        await db.SaveChangesAsync();
    }

    public static string CreateJwtToken(
        long userId,
        string role,
        string? roleCode = null,
        long? branchId = null,
        string securityStamp = "test-stamp")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role),
            new("security_stamp", securityStamp),
            new("auth_time_ticks", DateTime.UtcNow.Ticks.ToString())
        };

        if (!string.IsNullOrWhiteSpace(roleCode))
        {
            claims.Add(new Claim("role_code", roleCode));
        }

        if (branchId.HasValue)
        {
            claims.Add(new Claim("branch_id", branchId.Value.ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestJwtIssuer,
            audience: TestJwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void SeedBasicBranchAndUsers(GarageContext db)
    {
        if (!db.Branches.Any(b => b.Id == 1))
        {
            db.Branches.AddRange(
                new Branch { Id = 1, Name = "Chi nhánh 1", Status = "ACTIVE" },
                new Branch { Id = 2, Name = "Chi nhánh 2", Status = "ACTIVE" });
        }

        if (!db.Customers.Any(c => c.Id == 100))
        {
            db.Customers.Add(new Customer
            {
                Id = 100,
                FullName = "Nguyễn Văn Khách",
                Phone = "0901112233",
                Email = "customer100@example.com",
                PasswordHash = "hash",
                Type = "INDIVIDUAL",
                SecurityStamp = "test-stamp",
                IsActive = true
            });
        }

        if (!db.Staffs.Any(s => s.Id == 200))
        {
            db.Staffs.AddRange(
                new Staff
                {
                    Id = 200,
                    FullName = "Quản lý CN1",
                    Phone = "0902223344",
                    Email = "manager1@example.com",
                    PasswordHash = "hash",
                    BranchId = 1,
                    Status = "ACTIVE",
                    SecurityStamp = "test-stamp"
                },
                new Staff
                {
                    Id = 201,
                    FullName = "Admin Hệ Thống",
                    Phone = "0903334455",
                    Email = "admin@example.com",
                    PasswordHash = "hash",
                    BranchId = 1,
                    Status = "ACTIVE",
                    SecurityStamp = "test-stamp"
                });
        }
    }

    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendEmailAsync(string toEmail, string subject, string body) => Task.CompletedTask;
    }
}
