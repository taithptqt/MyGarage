using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Data;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public class PaymentControllerTransitionTests
{
    [Fact]
    public async Task TransitionStatus_ValidTransitionToCompleted_ReturnsOkAndUpdatesStatus()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.PaymentMethods.Add(new PaymentMethod { Id = 1, MethodCode = "CASH", MethodName = "Tiền mặt", IsActive = true });
            db.Payments.Add(new Payment
            {
                Id = 10,
                PayerType = "CUSTOMER",
                PayerId = 100,
                MethodId = 1,
                Amount = 250_000m,
                Direction = "INFLOW",
                Type = "PAYMENT",
                Status = "PENDING"
            });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));

        var response = await client.PostAsJsonAsync(
            "/api/v1/payments/10/status-transitions",
            new { status = "COMPLETED", amount = 250_000m, note = "Xác nhận thu tiền" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("confirmed").GetBoolean());
        Assert.Equal("COMPLETED", body.GetProperty("payment").GetProperty("status").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var saved = await db.Payments.FindAsync(10L);
        Assert.NotNull(saved);
        Assert.Equal("COMPLETED", saved!.Status);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("PROCESSING")]
    [InlineData("INVALID_STATUS")]
    [InlineData("")]
    public async Task TransitionStatus_InvalidTargetStatus_ReturnsBadRequest(string targetStatus)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.PaymentMethods.Add(new PaymentMethod { Id = 1, MethodCode = "CASH", MethodName = "Tiền mặt", IsActive = true });
            db.Payments.Add(new Payment
            {
                Id = 10,
                PayerType = "CUSTOMER",
                PayerId = 100,
                MethodId = 1,
                Amount = 100_000m,
                Direction = "INFLOW",
                Type = "PAYMENT",
                Status = "PENDING"
            });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "ACCOUNTANT", 1));

        var response = await client.PostAsJsonAsync(
            "/api/v1/payments/10/status-transitions",
            new { status = targetStatus });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("COMPLETED")]
    [InlineData("FAILED")]
    [InlineData("CANCELED")]
    public async Task TransitionStatus_ImmutablePayment_ReturnsBadRequest(string existingStatus)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.PaymentMethods.Add(new PaymentMethod { Id = 1, MethodCode = "CASH", MethodName = "Tiền mặt", IsActive = true });
            db.Payments.Add(new Payment
            {
                Id = 11,
                PayerType = "CUSTOMER",
                PayerId = 100,
                MethodId = 1,
                Amount = 100_000m,
                Direction = "INFLOW",
                Type = "PAYMENT",
                Status = existingStatus
            });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));

        var response = await client.PostAsJsonAsync(
            "/api/v1/payments/11/status-transitions",
            new { status = "COMPLETED" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TransitionStatus_PaymentNotFound_ReturnsNotFound()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));

        var response = await client.PostAsJsonAsync(
            "/api/v1/payments/99999/status-transitions",
            new { status = "COMPLETED" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TransitionStatus_UnauthorizedAndForbidden_EnforcesAuthAndBranchScope()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Staffs.Add(new Staff
            {
                Id = 202,
                FullName = "Kỹ thuật viên CN1",
                Phone = "0904445566",
                Email = "tech1@example.com",
                PasswordHash = "hash",
                BranchId = 1,
                Status = "ACTIVE",
                SecurityStamp = "test-stamp"
            });
            db.Staffs.Add(new Staff
            {
                Id = 203,
                FullName = "Quản lý CN2",
                Phone = "0905556677",
                Email = "manager2@example.com",
                PasswordHash = "hash",
                BranchId = 2,
                Status = "ACTIVE",
                SecurityStamp = "test-stamp"
            });
            db.CustomerVehicles.Add(new CustomerVehicle
            {
                Id = 1,
                CustomerId = 100,
                VehicleId = 1,
                IsCurrentOwner = true
            });
            db.ServiceOrders.Add(new ServiceOrder
            {
                Id = 50,
                Code = "SO-TEST-50",
                CustomerId = 100,
                VehicleId = 1,
                OriginBranchId = 1,
                ExecuteBranchId = 1,
                Status = "IN_PROGRESS"
            });
            db.PaymentMethods.Add(new PaymentMethod { Id = 1, MethodCode = "CASH", MethodName = "Tiền mặt", IsActive = true });
            db.Payments.Add(new Payment
            {
                Id = 12,
                ServiceOrderId = 50,
                PayerType = "CUSTOMER",
                PayerId = 100,
                MethodId = 1,
                Amount = 150_000m,
                Direction = "INFLOW",
                Type = "PAYMENT",
                Status = "PENDING"
            });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();

        // 1. Unauthenticated -> 401
        var unauthResponse = await client.PostAsJsonAsync(
            "/api/v1/payments/12/status-transitions",
            new { status = "COMPLETED" });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);

        // 2. Customer role -> 403
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer"));
        var customerResponse = await client.PostAsJsonAsync(
            "/api/v1/payments/12/status-transitions",
            new { status = "COMPLETED" });
        Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);

        // 3. Staff without allowed role keyword (TECHNICIAN) -> 403
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestWebApplicationFactory.CreateJwtToken(202, "Staff", "TECHNICIAN", 1));
        var techResponse = await client.PostAsJsonAsync(
            "/api/v1/payments/12/status-transitions",
            new { status = "COMPLETED" });
        Assert.Equal(HttpStatusCode.Forbidden, techResponse.StatusCode);

        // 4. Staff from different branch (branch 2 vs ServiceOrder branch 1) -> 403
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestWebApplicationFactory.CreateJwtToken(203, "Staff", "MANAGER", 2));
        var wrongBranchResponse = await client.PostAsJsonAsync(
            "/api/v1/payments/12/status-transitions",
            new { status = "COMPLETED" });
        Assert.Equal(HttpStatusCode.Forbidden, wrongBranchResponse.StatusCode);
    }
}
